using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using CWServiceBus.Faults;
using log4net;

namespace CWServiceBus.Transport {
    public class TransactionalTransport : ITransport {
        private int maxRetries = 5;
        private int numberOfWorkerThreads = 1;

        public int MaxRetries {
            get { return maxRetries; }
            set { maxRetries = value; }
        }

        public IReceiveMessages MessageReceiver { get; set; }
        public IManageMessageFailures FailureManager { get; set; }
        public ITransactionWrapper TransactionWrapper { get; set; }

        public event EventHandler<StartedMessageProcessingEventArgs> StartedMessageProcessing;
        public event EventHandler FinishedMessageProcessing;
        public event EventHandler<FailedMessageProcessingEventArgs> FailedMessageProcessing;
        public event EventHandler<TransportMessageReceivedEventArgs> TransportMessageReceived;


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
            MessageReceiver.Init();

            for (int i = 0; i < numberOfWorkerThreads; i++)
                AddWorkerThread().Start();
        }

        #region helper methods

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
                TransactionWrapper.RunInTransaction(transactionToken => {
                    ReceiveMessage(transactionToken);
                });
                ClearFailuresForMessage(messageId);
            } catch (AbortHandlingCurrentMessageException) {
                //in case AbortHandlingCurrentMessage was called
                return; //don't increment failures, we want this message kept around.
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

        public void ReceiveMessage(ITransactionToken transactionToken) {
            var m = Receive(transactionToken);
            if (m == null)
                return;
            ProcessMessage(m);
        }

        void ProcessMessage(TransportMessage m) {
            messageId = m.Id;

            var exceptionFromStartedMessageHandling = OnStartedMessageProcessing(m);

            if (HandledMaxRetries(m)) {
                Logger.Error(string.Format("Message has failed the maximum number of times allowed, ID={0}.", m.Id));
                OnFinishedMessageProcessing();
                return;
            }

            if (exceptionFromStartedMessageHandling != null)
                throw exceptionFromStartedMessageHandling; //cause rollback 

            //care about failures here
            var exceptionFromMessageHandling = OnTransportMessageReceived(m);

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

        private bool HandledMaxRetries(TransportMessage message) {
            string messageId = message.Id;
            failuresPerMessageLocker.EnterReadLock();

            if (failuresPerMessage.ContainsKey(messageId) &&
                (failuresPerMessage[messageId] >= maxRetries)) {
                failuresPerMessageLocker.ExitReadLock();
                failuresPerMessageLocker.EnterWriteLock();

                var ex = exceptionsForMessages[messageId];
                InvokeFaultManager(message, ex);
                failuresPerMessage.Remove(messageId);
                exceptionsForMessages.Remove(messageId);

                failuresPerMessageLocker.ExitWriteLock();

                return true;
            }

            failuresPerMessageLocker.ExitReadLock();
            return false;
        }

        void InvokeFaultManager(TransportMessage message, Exception exception) {
            try {
                FailureManager.ProcessingAlwaysFailsForMessage(message, exception);
            } catch (Exception ex) {
                Logger.FatalFormat("Fault manager failed to process the failed message {0}", ex, message);
                // TODO what's going on here/
                //Configure.Instance.OnCriticalError();
            }
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

        [DebuggerNonUserCode] // so that exceptions don't interfere with debugging.
        private TransportMessage Receive(ITransactionToken transactionToken) {
            try {
                return MessageReceiver.Receive(transactionToken);
            } catch (InvalidOperationException) {
                //TODO what's going on here?
                //Configure.Instance.OnCriticalError();
                return null;
            } catch (Exception e) {
                Logger.Error("Error in receiving messages.", e);
                return null;
            } finally {
                transactionWaitPool.Release(1);
                releasedWaitLock = true;
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

        #endregion

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


        private static readonly ILog Logger = LogManager.GetLogger(typeof(TransactionalTransport).Namespace);
    }
}
