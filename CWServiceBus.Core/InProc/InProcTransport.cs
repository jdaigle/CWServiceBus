using System;
using System.Collections.Generic;
using System.Linq;
using CWServiceBus.Transport;
using log4net;

namespace CWServiceBus.InProc
{
    public class InProcTransport : ITransport
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(InProcTransport));

        public event EventHandler<StartedMessageProcessingEventArgs> StartedMessageProcessing;
        public event EventHandler FinishedMessageProcessing;
        public event EventHandler<FailedMessageProcessingEventArgs> FailedMessageProcessing;
        public event EventHandler<TransportMessageReceivedEventArgs> TransportMessageReceived;
        public event EventHandler<TransportMessageFaultEventArgs> MessageFault;

        public void Start()
        {
            // nop
        }

        public void ChangeNumberOfWorkerThreads(int targetNumberOfWorkerThreads)
        {
            // nop
        }

        public int NumberOfWorkerThreads
        {
            get { return 0; }
        }

        public void AbortHandlingCurrentMessage()
        {
            throw new NotSupportedException();
        }

        public void Send(TransportMessage message, IEnumerable<string> destinations)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// This is a special helper method which will cause the transport to receive messages as if they came off the bus
        /// </summary>
        public void ReceiveMessages(object[] messages, IEnumerable<HeaderInfo> headers)
        {
            try
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

                OnStartedMessageProcessing(transportMessage);
                //care about failures here
                var exceptionFromMessageHandling = OnTransportMessageReceived(transportMessage);
                //and here
                var exceptionFromMessageModules = OnFinishedMessageProcessing();
                if (exceptionFromMessageHandling != null) //cause rollback
                    throw exceptionFromMessageHandling;
                if (exceptionFromMessageModules != null) //cause rollback
                    throw exceptionFromMessageModules;
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

        private void OnMessageFault(TransportMessage msg, Exception exception)
        {
            if (MessageFault != null)
                MessageFault(this, new TransportMessageFaultEventArgs(msg, exception, "Exception Thrown"));
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

    }
}
