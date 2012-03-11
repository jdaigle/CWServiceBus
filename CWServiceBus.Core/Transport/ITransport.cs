using System;
using System.Collections.Generic;

namespace CWServiceBus.Transport {
    public interface ITransport {
        void Start();

        int NumberOfWorkerThreads { get; }
        void ChangeNumberOfWorkerThreads(int targetNumberOfWorkerThreads);
        void Send(TransportMessage message, IEnumerable<string> destinations);
        
        event EventHandler<TransportMessageReceivedEventArgs> TransportMessageReceived;
        event EventHandler<StartedMessageProcessingEventArgs> StartedMessageProcessing;
        event EventHandler FinishedMessageProcessing;
        event EventHandler<FailedMessageProcessingEventArgs> FailedMessageProcessing;
        void AbortHandlingCurrentMessage();
    }
}
