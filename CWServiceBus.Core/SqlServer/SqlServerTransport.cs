using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Threading;
using CWServiceBus.Transport;
using log4net;
using System.Reflection;
using System.Text;

namespace CWServiceBus.SqlServer
{
    public class SqlServerTransport : ITransport
    {
        private int maxRetries = 5;
        private int numberOfWorkerThreads = 1;

        public string ListenerQueue { get; set; }
        public string ReturnAddress { get { return ListenerQueue; } }

        public int MaxRetries
        {
            get { return maxRetries; }
            set { maxRetries = value; }
        }

        public ISqlServerTransactionWrapper TransactionWrapper { get; set; }
        public IMessageSerializer MessageSerializer { get; set; }
        private ISet<string> faultForwardDestinations = new HashSet<string>();

        public event EventHandler<StartedMessageProcessingEventArgs> StartedMessageProcessing;
        public event EventHandler FinishedMessageProcessing;
        public event EventHandler<FailedMessageProcessingEventArgs> FailedMessageProcessing;
        public event EventHandler<TransportMessageReceivedEventArgs> TransportMessageReceived;
        public event EventHandler<TransportMessageFaultEventArgs> MessageFault;

        public SqlServerTransport() { }

        public SqlServerTransport(string listenerQueue, ISqlServerTransactionWrapper transactionWrapper, IMessageSerializer transportMessageSerializer, int initialNumberOfWorkThreads = 1)
        {
            this.ListenerQueue = listenerQueue.Trim();
            this.TransactionWrapper = transactionWrapper;
            this.MessageSerializer = transportMessageSerializer;
            this.numberOfWorkerThreads = initialNumberOfWorkThreads;
        }

        public virtual int NumberOfWorkerThreads
        {
            get
            {
                lock (workerThreads)
                    return workerThreads.Count;
            }
        }

        public void ChangeNumberOfWorkerThreads(int targetNumberOfWorkerThreads)
        {
            lock (workerThreads)
            {
                var current = workerThreads.Count;

                if (targetNumberOfWorkerThreads == current)
                    return;

                if (targetNumberOfWorkerThreads < current)
                {
                    for (var i = targetNumberOfWorkerThreads; i < current; i++)
                        workerThreads[i].Stop();

                    return;
                }

                if (targetNumberOfWorkerThreads > current)
                {
                    for (var i = current; i < targetNumberOfWorkerThreads; i++)
                        AddWorkerThread().Start();

                    return;
                }
            }
        }

        private bool hasStarted;
        private object startLock = new object();

        void ITransport.Start()
        {
            if (hasStarted)
            {
                return;
            }
            lock (startLock)
            {
                if (hasStarted)
                {
                    return;
                }
                InitSqlServerQueue();
                for (int i = 0; i < numberOfWorkerThreads; i++)
                {
                    AddWorkerThread().Start();
                }
                hasStarted = true;
            }
        }

        private void InitSqlServerQueue()
        {
            TransactionWrapper.RunInTransaction(transaction =>
            {
                using (var cmd = transaction.Connection.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = string.Format(SqlCommands.CreateQueueTable, ListenerQueue.Trim());
                    cmd.ExecuteNonQuery();
                }
            });
        }

        private WorkerThread AddWorkerThread()
        {
            lock (workerThreads)
            {
                var result = new WorkerThread(Process);
                workerThreads.Add(result);
                result.Stopped += delegate(object sender, EventArgs e)
                {
                    var wt = sender as WorkerThread;
                    lock (workerThreads)
                        workerThreads.Remove(wt);
                };

                return result;
            }
        }

        private void Process()
        {
            releasedWaitLock = false;
            needToAbort = false;
            messageId = string.Empty;

            try
            {
                transactionWaitPool.WaitOne();
                TransactionWrapper.RunInTransaction(transaction =>
                {
                    ReceiveMessage(transaction);
                });
                ClearFailuresForMessage(messageId);
            }
            catch (AbortHandlingCurrentMessageException)
            {
                //in case AbortHandlingCurrentMessage was called
                //don't increment failures, we want this message kept around.
                return;
            }
            catch (Exception e)
            {
                var originalException = e;

                if (e is TransportMessageHandlingFailedException)
                    originalException = ((TransportMessageHandlingFailedException)e).OriginalException;

                IncrementFailuresForMessage(messageId, originalException);
                Logger.Error("Error Procesing Message", originalException);
                OnFailedMessageProcessing(originalException);
            }
            finally
            {
                if (!releasedWaitLock)
                {
                    transactionWaitPool.Release(1);
                }
            }
        }

        public void ReceiveMessage(SqlTransaction transaction)
        {
            TransportMessage message = null;
            Guid id;
            try
            {
                using (var cmd = transaction.Connection.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = string.Format(SqlCommands.SelectMessage, ListenerQueue);
                    var backoff = new BackOff(1000);
                    while (message == null)
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                message = new TransportMessage();
                                id = reader.GetGuid(0);
                                message.Id = id.ToString();
                                message.CorrelationId = reader.GetString(1) ?? string.Empty;
                                message.ReturnAddress = reader.GetString(2);
                                message.MessageIntent = (MessageIntentEnum)reader.GetByte(3);
                                message.HeadersString = reader.GetString(4) ?? string.Empty;
                                message.BodyString = reader.GetString(5) ?? string.Empty;
                                try
                                {
                                    using (var bodyStream = new MemoryStream(Encoding.UTF8.GetBytes(message.BodyString)))
                                    {
                                        message.Body = MessageSerializer.Deserialize(bodyStream);
                                    }
                                    if (message.HeadersString.Length > 0)
                                    {
                                        using (var headersStream = new MemoryStream(Encoding.UTF8.GetBytes(message.HeadersString)))
                                        {
                                            message.Headers = MessageSerializer.Deserialize(headersStream).Cast<HeaderInfo>().ToList();
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Logger.Error("Could not extract message data.", ex);
                                    OnSerializationFailed(id, message, message.BodyString, message.HeadersString, ex);
                                    return; // deserialization failed - no reason to try again, so don't throw
                                }
                            }
                        }
                        if (message == null)
                        {
                            backoff.Wait(() => true);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error("Error in receiving message from queue.", e);
                throw; // Throw to rollback 
            }
            finally
            {
                transactionWaitPool.Release(1);
                releasedWaitLock = true;
            }

            // No message? That's okay
            if (message == null)
            {
                return;
            }

            // Set the correlation Id
            if (string.IsNullOrEmpty(message.IdForCorrelation))
            {
                message.IdForCorrelation = message.Id;
            }
            ProcessMessage(message);
        }

        private void OnSerializationFailed(Guid messageId, TransportMessage transportMessage, string body, string headers, Exception exception)
        {
            try
            {
                this.WriteFailedMessage(transportMessage, exception, body, headers);
                if (MessageFault != null)
                    MessageFault(this, new TransportMessageFaultEventArgs(transportMessage, exception, "SerializationFailed"));
                SendFailureMessage(transportMessage, exception, "SerializationFailed");
            }
            catch (Exception e)
            {
                Logger.FatalFormat("Fault manager failed to process the failed message {0}", e);
                // TODO critical error will stop the transport from handling new messages
                //Configure.Instance.OnCriticalError();
            }
        }

        private void ProcessMessage(TransportMessage transportMessage)
        {
            messageId = transportMessage.Id;

            var exceptionFromStartedMessageHandling = OnStartedMessageProcessing(transportMessage);

            Exception lastException = null;
            if (HandledMaxRetries(transportMessage, out lastException))
            {
                try
                {
                    this.WriteFailedMessage(transportMessage, lastException, transportMessage.BodyString, transportMessage.HeadersString);
                    if (MessageFault != null)
                        MessageFault(this, new TransportMessageFaultEventArgs(transportMessage, lastException, "ProcessingFailed"));
                    SendFailureMessage(transportMessage, lastException, "ProcessingFailed");
                }
                catch (Exception e)
                {
                    Logger.FatalFormat("Fault manager failed to process the failed message {0}", e, transportMessage);
                    // TODO critical error will stop the transport from handling new messages
                    //Configure.Instance.OnCriticalError();
                }
                Logger.Error(string.Format("Message has failed the maximum number of times allowed, ID={0}.", transportMessage.Id));
                OnFinishedMessageProcessing();
                return;
            }

            if (exceptionFromStartedMessageHandling != null)
                throw exceptionFromStartedMessageHandling; //cause rollback 

            //care about failures here
            var exceptionFromMessageHandling = OnTransportMessageReceived(transportMessage);

            //and here
            var exceptionFromMessageModules = OnFinishedMessageProcessing();

            //but need to abort takes precedence - failures aren't counted here,
            //so messages aren't moved to the error queue.
            if (needToAbort)
                throw new AbortHandlingCurrentMessageException();

            if (exceptionFromMessageHandling != null) //cause rollback
                throw exceptionFromMessageHandling;

            if (exceptionFromMessageModules != null) //cause rollback
                throw exceptionFromMessageModules;
        }

        /// <summary>
        /// This is a special helper method which will cause the transport to receive messages as if they came off the bus
        /// </summary>
        public void ReceiveMessages(object[] messages, IEnumerable<HeaderInfo> headers)
        {
            needToAbort = false;
            messageId = string.Empty;
            try
            {
                TransactionWrapper.RunInTransaction(transaction =>
                {

                    var transportMessage = new TransportMessage()
                    {
                        Id = Guid.NewGuid().ToString(),
                        IdForCorrelation = Guid.NewGuid().ToString(),
                        Body = messages,
                        TimeSent = DateTime.UtcNow,
                        MessageIntent = MessageIntentEnum.Send,
                        Headers = headers.ToList(),
                    };

                    //care about failures here
                    var exceptionFromMessageHandling = OnTransportMessageReceived(transportMessage);

                    //and here
                    var exceptionFromMessageModules = OnFinishedMessageProcessing();

                    //but need to abort takes precedence - failures aren't counted here,
                    //so messages aren't moved to the error queue.
                    if (needToAbort)
                        throw new AbortHandlingCurrentMessageException();

                    if (exceptionFromMessageHandling != null) //cause rollback
                        throw exceptionFromMessageHandling;

                    if (exceptionFromMessageModules != null) //cause rollback
                        throw exceptionFromMessageModules;
                });
            }
            catch (AbortHandlingCurrentMessageException)
            {
                //in case AbortHandlingCurrentMessage was called
                //don't increment failures, we want this message kept around.
                return;
            }
            catch (Exception e)
            {
                var originalException = e;
                if (originalException is TransportMessageHandlingFailedException)
                    originalException = ((TransportMessageHandlingFailedException)e).OriginalException;
                OnFailedMessageProcessing(originalException);
                throw;
            }
        }

        private bool HandledMaxRetries(TransportMessage message, out Exception lastException)
        {
            string messageId = message.Id;
            failuresPerMessageLocker.EnterReadLock();

            if (failuresPerMessage.ContainsKey(messageId) &&
                (failuresPerMessage[messageId] >= maxRetries))
            {
                failuresPerMessageLocker.ExitReadLock();
                failuresPerMessageLocker.EnterWriteLock();

                lastException = exceptionsForMessages[messageId];
                failuresPerMessage.Remove(messageId);
                exceptionsForMessages.Remove(messageId);

                failuresPerMessageLocker.ExitWriteLock();

                return true;
            }

            lastException = null;
            failuresPerMessageLocker.ExitReadLock();
            return false;
        }

        private void ClearFailuresForMessage(string messageId)
        {
            failuresPerMessageLocker.EnterReadLock();
            if (failuresPerMessage.ContainsKey(messageId))
            {
                failuresPerMessageLocker.ExitReadLock();
                failuresPerMessageLocker.EnterWriteLock();

                failuresPerMessage.Remove(messageId);
                exceptionsForMessages.Remove(messageId);

                failuresPerMessageLocker.ExitWriteLock();
            }
            else
                failuresPerMessageLocker.ExitReadLock();
        }

        private void IncrementFailuresForMessage(string messageId, Exception e)
        {
            try
            {
                failuresPerMessageLocker.EnterWriteLock();

                if (!failuresPerMessage.ContainsKey(messageId))
                    failuresPerMessage[messageId] = 1;
                else
                    failuresPerMessage[messageId] = failuresPerMessage[messageId] + 1;

                exceptionsForMessages[messageId] = e;
            }
            catch { } //intentionally swallow exceptions here
            finally
            {
                failuresPerMessageLocker.ExitWriteLock();
            }
        }

        /// <summary>
        /// Causes the processing of the current message to be aborted.
        /// </summary>
        public void AbortHandlingCurrentMessage()
        {
            needToAbort = true;
        }

        private Exception OnStartedMessageProcessing(TransportMessage msg)
        {
            try
            {
                if (StartedMessageProcessing != null)
                    StartedMessageProcessing(this, new StartedMessageProcessingEventArgs(msg));
            }
            catch (Exception e)
            {
                Logger.Error("Failed raising 'started message processing' event.", e);
                return e;
            }

            return null;
        }


        private Exception OnFinishedMessageProcessing()
        {
            try
            {
                if (FinishedMessageProcessing != null)
                    FinishedMessageProcessing(this, null);
            }
            catch (Exception e)
            {
                Logger.Error("Failed raising 'finished message processing' event.", e);
                return e;
            }

            return null;
        }

        private Exception OnTransportMessageReceived(TransportMessage msg)
        {
            try
            {
                if (TransportMessageReceived != null)
                    TransportMessageReceived(this, new TransportMessageReceivedEventArgs(msg));
            }
            catch (Exception e)
            {
                var originalException = e;
                if (e is TransportMessageHandlingFailedException)
                    originalException = ((TransportMessageHandlingFailedException)e).OriginalException;
                Logger.Warn("Failed raising 'transport message received' event for message with ID=" + msg.Id, originalException);
                return e;
            }
            return null;
        }

        private bool OnFailedMessageProcessing(Exception originalException)
        {
            try
            {
                if (FailedMessageProcessing != null)
                    FailedMessageProcessing(this, new FailedMessageProcessingEventArgs(originalException));
            }
            catch (Exception e)
            {
                Logger.Warn("Failed raising 'failed message processing' event.", e);
                return false;
            }

            return true;
        }

        private readonly IList<WorkerThread> workerThreads = new List<WorkerThread>();

        /// <summary>
        /// Accessed by multiple threads - lock using failuresPerMessageLocker.
        /// </summary>
        private readonly IDictionary<string, int> failuresPerMessage = new Dictionary<string, int>();
        private readonly IDictionary<string, Exception> exceptionsForMessages = new Dictionary<string, Exception>();
        private readonly ReaderWriterLockSlim failuresPerMessageLocker = new ReaderWriterLockSlim();

        [ThreadStatic]
        private static volatile bool needToAbort;
        [ThreadStatic]
        private static volatile string messageId;
        [ThreadStatic]
        private static volatile bool releasedWaitLock;

        private readonly Semaphore transactionWaitPool = new Semaphore(1, 1);


        private static readonly ILog Logger = LogManager.GetLogger(typeof(SqlServerTransport).Namespace);


        public void Send(TransportMessage toSend, IEnumerable<string> destinations)
        {
            var destinationQueue = string.Empty;
            try
            {
                TransactionWrapper.RunInTransaction(transaction =>
                {
                    var id = Guid.NewGuid();
                    toSend.Id = id.ToString();
                    toSend.TimeSent = DateTime.UtcNow;
                    toSend.ReturnAddress = this.ListenerQueue;
                    var serializedMessage = string.Empty;
                    var messages = "";
                    var headers = "";
                    using (var stream = new MemoryStream())
                    {
                        MessageSerializer.Serialize(toSend.Body, stream);
                        messages = Encoding.UTF8.GetString(stream.ToArray());
                    }
                    if (toSend.Headers != null && toSend.Headers.Count > 0)
                    {
                        using (var stream = new MemoryStream())
                        {
                            MessageSerializer.Serialize(toSend.Headers.ToArray(), stream);
                            headers = Encoding.UTF8.GetString(stream.ToArray());
                        }
                    }
                    using (var cmd = transaction.Connection.CreateCommand())
                    {
                        cmd.Transaction = transaction;
                        cmd.Parameters.AddWithValue("@Id", id);
                        var paramCorrelationId = cmd.Parameters.AddWithValue("@CorrelationId", toSend.CorrelationId ?? string.Empty);
                        paramCorrelationId.SqlDbType = SqlDbType.VarChar;
                        paramCorrelationId.Size = 255;
                        var paramReplyToAddress = cmd.Parameters.AddWithValue("@ReplyToAddress", this.ListenerQueue);
                        paramReplyToAddress.SqlDbType = SqlDbType.VarChar;
                        paramReplyToAddress.Size = 255;
                        cmd.Parameters.AddWithValue("@MessageIntent", (byte)toSend.MessageIntent);

                        var paramHeaders = cmd.Parameters.AddWithValue("@Headers", headers);
                        paramHeaders.Size = -1;
                        var paramBody = cmd.Parameters.AddWithValue("@Body", messages);
                        paramBody.Size = -1;
                        foreach (var destination in destinations)
                        {
                            destinationQueue = destination;
                            cmd.CommandText = string.Format(SqlCommands.InsertMessage, destination);
                            cmd.ExecuteNonQuery();
                            if (Logger.IsDebugEnabled)
                                Logger.Debug(string.Format("Sending message {0} with ID {1} to destination {2}.\n" +
                                                   "ToString() of the message yields: {3}\n" +
                                                   "Message headers:\n{4}",
                                                   toSend.Body[0].GetType().AssemblyQualifiedName,
                                                   toSend.Id,
                                                   destination,
                                                   toSend.Body[0],
                                                   string.Join(", ", toSend.Headers.Select(h => h.Key + ":" + h.Value).ToArray())
                            ));
                        }
                    }
                });
            }
            catch (SqlException ex)
            {
                if (ex.Number == 208)
                {
                    var msg = string.Format("Failed to send message to address: [{0}]", destinationQueue);
                    throw new QueueNotFoundException(toSend, msg, ex);
                }
            }
        }


        private void WriteFailedMessage(TransportMessage transportMessage, Exception exception, string originalBody, string originalHeaders)
        {
            try
            {
                // Write a failed message
                TransactionWrapper.RunInTransaction(transaction =>
                {
                    using (var command = transaction.Connection.CreateCommand())
                    {
                        command.CommandText = SqlCommands.InsertPoisonMessage;
                        var paramQueue = command.Parameters.AddWithValue("@Queue", this.ListenerQueue);
                        paramQueue.Size = 255;
                        command.Parameters.AddWithValue("@InsertDateTime", DateTime.UtcNow);
                        command.Parameters.AddWithValue("@Id", Guid.Parse(transportMessage.Id));
                        var paramCorrelationId = command.Parameters.AddWithValue("@CorrelationId", transportMessage.CorrelationId ?? string.Empty);
                        paramCorrelationId.SqlDbType = SqlDbType.VarChar;
                        paramCorrelationId.Size = 255;
                        var paramReplyToAddress = command.Parameters.AddWithValue("@ReplyToAddress", transportMessage.ReturnAddress);
                        paramReplyToAddress.SqlDbType = SqlDbType.VarChar;
                        paramReplyToAddress.Size = 255;
                        command.Parameters.AddWithValue("@MessageIntent", (byte)transportMessage.MessageIntent);
                        var paramHeaders = command.Parameters.AddWithValue("@Headers", originalHeaders ?? string.Empty);
                        paramHeaders.Size = -1;
                        var paramBody = command.Parameters.AddWithValue("@Body", originalBody ?? string.Empty);
                        paramBody.Size = -1;
                        if (exception != null)
                        {
                            var paramException = command.Parameters.AddWithValue("@Exception", FormatErrorMessage(exception));
                            paramException.Size = -1;
                        }
                        else
                        {
                            command.Parameters.AddWithValue("@Exception", DBNull.Value);
                        }
                        command.Transaction = transaction;
                        command.ExecuteNonQuery();
                    }
                });
            }
            catch (Exception e)
            {
                Logger.Fatal("Failed to write ServiceBroker Error Message", e);
                // suppress -- don't let this exception take down the process
            }
        }

        private static string FormatErrorMessage(Exception e)
        {
            var message = e.GetType().ToString() + ": " + e.Message + Environment.NewLine + e.StackTrace;
            if (e.InnerException != null)
            {
                message = message + Environment.NewLine + Environment.NewLine + FormatErrorMessage(e.InnerException);
            }
            return message;
        }

        public void ForwardFaultsTo(IEnumerable<string> faultForwardDestinations)
        {
            foreach (var item in faultForwardDestinations)
            {
                this.faultForwardDestinations.Add(item);
            }
        }

        private void SendFailureMessage(TransportMessage message, Exception e, string reason)
        {
            if (message == null)
                return;
            message.MessageIntent = MessageIntentEnum.FaultNotification;
            SetExceptionHeaders(message, e, reason);
            try
            {
                Send(message, this.faultForwardDestinations);
            }
            catch (Exception exception)
            {
                var errorMessage = string.Format("Could not forward failed message to error queue, reason: {0}.", exception.ToString());
                Logger.Fatal(errorMessage, exception);
                throw new InvalidOperationException(errorMessage, exception);
            }
        }

        private void SetExceptionHeaders(TransportMessage message, Exception e, string reason)
        {
            if (message.Headers == null)
                message.Headers = new List<HeaderInfo>();
            message.Headers.Add(new HeaderInfo() { Key = "CWServiceBus.ExceptionInfo.Reason", Value = reason });
            message.Headers.Add(new HeaderInfo() { Key = "CWServiceBus.ExceptionInfo.ExceptionType", Value = e.GetType().FullName });

            if (e.InnerException != null)
                message.Headers.Add(new HeaderInfo() { Key = "CWServiceBus.ExceptionInfo.InnerExceptionType", Value = e.InnerException.GetType().FullName });

            message.Headers.Add(new HeaderInfo() { Key = "CWServiceBus.ExceptionInfo.HelpLink", Value = e.HelpLink });
            message.Headers.Add(new HeaderInfo() { Key = "CWServiceBus.ExceptionInfo.Message", Value = e.Message });
            message.Headers.Add(new HeaderInfo() { Key = "CWServiceBus.ExceptionInfo.Source", Value = e.Source });
            message.Headers.Add(new HeaderInfo() { Key = "CWServiceBus.ExceptionInfo.StackTrace", Value = e.StackTrace });

            message.Headers.Add(new HeaderInfo() { Key = "CWServiceBus.OriginalId", Value = message.Id });
            message.Headers.Add(new HeaderInfo() { Key = "CWServiceBus.FaultedAddress", Value = this.ListenerQueue });
            message.Headers.Add(new HeaderInfo() { Key = "CWServiceBus.SqlServer.FaultedQueue", Value = this.ListenerQueue });
            message.Headers.Add(new HeaderInfo() { Key = "CWServiceBus.TimeOfFailure", Value = DateTime.UtcNow.ToString("o") });

        }
    }
}
