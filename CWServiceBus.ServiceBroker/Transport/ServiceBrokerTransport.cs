using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Threading;
using CWServiceBus.Faults;
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

        public IManageMessageFailures FailureManager { get; set; }
        public ISqlServerTransactionWrapper TransactionWrapper { get; set; }
        public ITransportMessageSerializer TransportMessageSerializer { get; set; }

        public event EventHandler<StartedMessageProcessingEventArgs> StartedMessageProcessing;
        public event EventHandler FinishedMessageProcessing;
        public event EventHandler<FailedMessageProcessingEventArgs> FailedMessageProcessing;
        public event EventHandler<TransportMessageReceivedEventArgs> TransportMessageReceived;

        public ServiceBrokerTransport() { }

        public ServiceBrokerTransport(string listenerQueue, string returnAddress, SqlServerTransactionWrapper transactionWrapper, ITransportMessageSerializer transportMessageSerializer, IManageMessageFailures failureManager, int initialNumberOfWorkThreads = 1) {
            this.ListenerQueue = listenerQueue;
            this.ReturnAddress = returnAddress;
            this.TransactionWrapper = transactionWrapper;
            this.TransportMessageSerializer = transportMessageSerializer;
            this.FailureManager = failureManager;
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

        private void OnSerializationFailed(Guid conversationHandle, Message message, Exception exception) {
            try {
                if (FailureManager != null)
                    FailureManager.SerializationFailedForMessage(message, null, exception);
            } catch (Exception e) {
                Logger.FatalFormat("Fault manager failed to process the failed message {0}", e, message);
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
                    if (FailureManager != null)
                        FailureManager.ProcessingAlwaysFailsForMessage(underlyingTransportObject, transportMessage, lastException);
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
    }
}
