using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Threading;
using CWServiceBus.Transport;
using log4net;

namespace CWServiceBus.ServiceBroker.Transport {
    public class ServiceBrokerTransport : ITransport {

        public const string NServiceBusTransportMessageContract = "NServiceBusTransportMessageContract";
        public const string NServiceBusTransportMessage = "NServiceBusTransportMessage";

        private int maxRetries = 5;
        private int numberOfWorkerThreads = 1;

        private static readonly int waitTimeout = 21600 * 1000; // wait 6 hours
        public string ListenerQueue { get; set; }
        public string ReturnAddress { get; set; }

        public int MaxRetries {
            get { return maxRetries; }
            set { maxRetries = value; }
        }

        public ISqlServerTransactionWrapper TransactionWrapper { get; set; }
        public ITransportMessageSerializer TransportMessageSerializer { get; set; }
        private ISet<string> faultForwardDestinations = new HashSet<string>();

        public event EventHandler<StartedMessageProcessingEventArgs> StartedMessageProcessing;
        public event EventHandler FinishedMessageProcessing;
        public event EventHandler<FailedMessageProcessingEventArgs> FailedMessageProcessing;
        public event EventHandler<TransportMessageReceivedEventArgs> TransportMessageReceived;
        public event EventHandler<TransportMessageFaultEventArgs> MessageFault;

        public ServiceBrokerTransport() { }

        public ServiceBrokerTransport(string listenerQueue, string returnAddress, SqlServerTransactionWrapper transactionWrapper, ITransportMessageSerializer transportMessageSerializer, int initialNumberOfWorkThreads = 1) {
            this.ListenerQueue = listenerQueue;
            this.ReturnAddress = returnAddress;
            this.TransactionWrapper = transactionWrapper;
            this.TransportMessageSerializer = transportMessageSerializer;
            this.numberOfWorkerThreads = initialNumberOfWorkThreads;
        }

        public virtual int NumberOfWorkerThreads {
            get {
                lock (workerThreads)
                    return workerThreads.Count;
            }
        }

        public void ChangeNumberOfWorkerThreads(int targetNumberOfWorkerThreads) {
            lock (workerThreads) {
                var current = workerThreads.Count;

                if (targetNumberOfWorkerThreads == current)
                    return;

                if (targetNumberOfWorkerThreads < current) {
                    for (var i = targetNumberOfWorkerThreads; i < current; i++)
                        workerThreads[i].Stop();

                    return;
                }

                if (targetNumberOfWorkerThreads > current) {
                    for (var i = current; i < targetNumberOfWorkerThreads; i++)
                        AddWorkerThread().Start();

                    return;
                }
            }
        }

        void ITransport.Start() {
            InitServiceBroker();
            for (int i = 0; i < numberOfWorkerThreads; i++)
                AddWorkerThread().Start();
        }

        private void InitServiceBroker() {
            TransactionWrapper.RunInTransaction(transaction => {
                // Ensure the service and queue exist
                ServiceBrokerWrapper.CreateServiceAndQueue(transaction, ReturnAddress, ListenerQueue);
            });
        }

        private WorkerThread AddWorkerThread() {
            lock (workerThreads) {
                var result = new WorkerThread(Process);
                workerThreads.Add(result);
                result.Stopped += delegate(object sender, EventArgs e) {
                    var wt = sender as WorkerThread;
                    lock (workerThreads)
                        workerThreads.Remove(wt);
                };

                return result;
            }
        }

        private void Process() {
            releasedWaitLock = false;
            needToAbort = false;
            messageId = string.Empty;

            try {
                transactionWaitPool.WaitOne();
                TransactionWrapper.RunInTransaction(transaction => {
                    ReceiveMessage(transaction);
                });
                ClearFailuresForMessage(messageId);
            } catch (AbortHandlingCurrentMessageException) {
                //in case AbortHandlingCurrentMessage was called
                //don't increment failures, we want this message kept around.
                return;
            } catch (Exception e) {
                var originalException = e;

                if (e is TransportMessageHandlingFailedException)
                    originalException = ((TransportMessageHandlingFailedException)e).OriginalException;

                IncrementFailuresForMessage(messageId, originalException);

                OnFailedMessageProcessing(originalException);
            } finally {
                if (!releasedWaitLock) {
                    transactionWaitPool.Release(1);
                }
            }
        }

        public void ReceiveMessage(SqlTransaction transaction) {
            Message message = null;
            try {
                message = ServiceBrokerWrapper.WaitAndReceive(transaction, this.ListenerQueue, waitTimeout); // Wait 6 hours
            } catch (Exception e) {
                Logger.Error("Error in receiving message from queue.", e);
                throw; // Throw to rollback 
            } finally {
                transactionWaitPool.Release(1);
                releasedWaitLock = true;
            }

            // No message? That's okay
            if (message == null)
                return;

            Guid conversationHandle = message.ConversationHandle;
            try {
                // Only handle transport messages
                if (message.MessageTypeName == NServiceBusTransportMessage) {

                    TransportMessage transportMessage = null;
                    try {
                        transportMessage = TransportMessageSerializer.Deserialize(message.BodyStream);
                    } catch (Exception ex) {
                        Logger.Error("Could not extract message data.", ex);
                        OnSerializationFailed(conversationHandle, message, ex);
                        return; // deserialization failed - no reason to try again, so don't throw
                    }

                    // Set the message Id
                    if (string.IsNullOrEmpty(transportMessage.Id))
                        transportMessage.Id = conversationHandle.ToString();

                    // Set the correlation Id
                    if (string.IsNullOrEmpty(transportMessage.IdForCorrelation))
                        transportMessage.IdForCorrelation = transportMessage.Id;

                    ProcessMessage(message, transportMessage);
                }
            } finally {
                // End the conversation
                ServiceBrokerWrapper.EndConversation(transaction, conversationHandle);
            }
        }

        private void OnSerializationFailed(Guid conversationHandle, Message underlyingTransportObject, Exception exception) {
            try {
                this.WriteFailedMessage(conversationHandle, underlyingTransportObject, null, exception, 3);
                if (MessageFault != null)
                    MessageFault(this, new TransportMessageFaultEventArgs(null, exception, "SerializationFailed"));
                SendFailureMessage(null, exception, "SerializationFailed");
            } catch (Exception e) {
                Logger.FatalFormat("Fault manager failed to process the failed message {0}", e, underlyingTransportObject);
                // TODO critical error will stop the transport from handling new messages
                //Configure.Instance.OnCriticalError();
            }
        }

        private void ProcessMessage(Message underlyingTransportObject, TransportMessage transportMessage) {
            messageId = transportMessage.Id;

            var exceptionFromStartedMessageHandling = OnStartedMessageProcessing(transportMessage);

            Exception lastException = null;
            if (HandledMaxRetries(transportMessage, out lastException)) {
                try {
                    this.WriteFailedMessage(underlyingTransportObject.ConversationHandle, underlyingTransportObject, transportMessage, lastException, 2);
                    if (MessageFault != null)
                        MessageFault(this, new TransportMessageFaultEventArgs(transportMessage, lastException, "ProcessingFailed"));
                    SendFailureMessage(transportMessage, lastException, "ProcessingFailed");
                } catch (Exception e) {
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

        private bool HandledMaxRetries(TransportMessage message, out Exception lastException) {
            string messageId = message.Id;
            failuresPerMessageLocker.EnterReadLock();

            if (failuresPerMessage.ContainsKey(messageId) &&
                (failuresPerMessage[messageId] >= maxRetries)) {
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

        private void ClearFailuresForMessage(string messageId) {
            failuresPerMessageLocker.EnterReadLock();
            if (failuresPerMessage.ContainsKey(messageId)) {
                failuresPerMessageLocker.ExitReadLock();
                failuresPerMessageLocker.EnterWriteLock();

                failuresPerMessage.Remove(messageId);
                exceptionsForMessages.Remove(messageId);

                failuresPerMessageLocker.ExitWriteLock();
            } else
                failuresPerMessageLocker.ExitReadLock();
        }

        private void IncrementFailuresForMessage(string messageId, Exception e) {
            try {
                failuresPerMessageLocker.EnterWriteLock();

                if (!failuresPerMessage.ContainsKey(messageId))
                    failuresPerMessage[messageId] = 1;
                else
                    failuresPerMessage[messageId] = failuresPerMessage[messageId] + 1;

                exceptionsForMessages[messageId] = e;
            } catch { } //intentionally swallow exceptions here
            finally {
                failuresPerMessageLocker.ExitWriteLock();
            }
        }

        /// <summary>
        /// Causes the processing of the current message to be aborted.
        /// </summary>
        public void AbortHandlingCurrentMessage() {
            needToAbort = true;
        }

        private Exception OnStartedMessageProcessing(TransportMessage msg) {
            try {
                if (StartedMessageProcessing != null)
                    StartedMessageProcessing(this, new StartedMessageProcessingEventArgs(msg));
            } catch (Exception e) {
                Logger.Error("Failed raising 'started message processing' event.", e);
                return e;
            }

            return null;
        }


        private Exception OnFinishedMessageProcessing() {
            try {
                if (FinishedMessageProcessing != null)
                    FinishedMessageProcessing(this, null);
            } catch (Exception e) {
                Logger.Error("Failed raising 'finished message processing' event.", e);
                return e;
            }

            return null;
        }

        private Exception OnTransportMessageReceived(TransportMessage msg) {
            try {
                if (TransportMessageReceived != null)
                    TransportMessageReceived(this, new TransportMessageReceivedEventArgs(msg));
            } catch (Exception e) {
                Logger.Warn("Failed raising 'transport message received' event for message with ID=" + msg.Id, e);

                return e;
            }

            return null;
        }

        private bool OnFailedMessageProcessing(Exception originalException) {
            try {
                if (FailedMessageProcessing != null)
                    FailedMessageProcessing(this, new FailedMessageProcessingEventArgs(originalException));
            } catch (Exception e) {
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


        private static readonly ILog Logger = LogManager.GetLogger(typeof(ServiceBrokerTransport).Namespace);


        public void Send(TransportMessage toSend, IEnumerable<string> destinations) {
            TransactionWrapper.RunInTransaction(transaction => {
                toSend.TimeSent = DateTime.UtcNow;
                toSend.ReturnAddress = this.ReturnAddress;
                var serializedMessage = string.Empty;
                using (var stream = new MemoryStream()) {
                    TransportMessageSerializer.Serialize(toSend, stream);
                    foreach (var destination in destinations) {
                        var conversationHandle = ServiceBrokerWrapper.SendOne(transaction, ReturnAddress, destination, NServiceBusTransportMessageContract, NServiceBusTransportMessage, stream.ToArray());
                        toSend.Id = conversationHandle.ToString();
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


        private void WriteFailedMessage(Guid messageId, Message serviceBrokerMessage, TransportMessage transportMessage, Exception exception, int status) {
            try {
                // Write a failed message
                TransactionWrapper.RunInTransaction(transaction => {
                    using (var command = transaction.Connection.CreateCommand()) {
                        command.CommandText = "cw_InsertFailedMessage";
                        command.CommandType = CommandType.StoredProcedure;
                        if (transportMessage != null)
                            command.Parameters.AddWithValue("@originService", transportMessage.ReturnAddress);
                        else
                            command.Parameters.AddWithValue("@originService", string.Empty);
                        command.Parameters.AddWithValue("@queueName", this.ListenerQueue);
                        command.Parameters.AddWithValue("@queueService", this.ReturnAddress);
                        command.Parameters.AddWithValue("@messageId", messageId);
                        command.Parameters.AddWithValue("@messageStatus", (int)status);
                        command.Parameters.AddWithValue("@messageData", serviceBrokerMessage.Body);
                        if (exception != null)
                            command.Parameters.AddWithValue("@errorMessage", FormatErrorMessage(exception));
                        else
                            command.Parameters.AddWithValue("@errorMessage", DBNull.Value);
                        command.Transaction = transaction;
                        command.ExecuteNonQuery();
                    }
                });
            } catch (Exception e) {
                Logger.Fatal("Failed to write ServiceBroker Error Message", e);
                // suppress -- don't let this exception take down the process
            }
        }

        private static string FormatErrorMessage(Exception e) {
            var message = "[" + e.GetType().ToString() + ": " + e.Message + "] " + Environment.NewLine + e.StackTrace;
            if (e.InnerException != null)
                message = message + Environment.NewLine + Environment.NewLine + FormatErrorMessage(e.InnerException);
            return message;
        }

        public void ForwardFaultsTo(IEnumerable<string> faultForwardDestinations) {
            foreach (var item in faultForwardDestinations) {
                this.faultForwardDestinations.Add(item);
            }
        }

        private void SendFailureMessage(TransportMessage message, Exception e, string reason) {
            if (message == null)
                return;
            message.MessageIntent = MessageIntentEnum.FaultNotification;
            SetExceptionHeaders(message, e, reason);
            try {
                Send(message, this.faultForwardDestinations);
            } catch (Exception exception) {
                var errorMessage = string.Format("Could not forward failed message to error queue, reason: {0}.", exception.ToString());
                Logger.Fatal(errorMessage, exception);
                throw new InvalidOperationException(errorMessage, exception);
            }
        }

        private void SetExceptionHeaders(TransportMessage message, Exception e, string reason) {
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
            message.Headers.Add(new HeaderInfo() { Key = "CWServiceBus.FaultedAddress", Value = this.ReturnAddress });
            message.Headers.Add(new HeaderInfo() { Key = "CWServiceBus.ServiceBroker.FaultedService", Value = this.ReturnAddress });
            message.Headers.Add(new HeaderInfo() { Key = "CWServiceBus.ServiceBroker.FaultedQueue", Value = this.ListenerQueue });
            message.Headers.Add(new HeaderInfo() { Key = "CWServiceBus.TimeOfFailure", Value = DateTime.UtcNow.ToString("o") });

        }
    }
}
