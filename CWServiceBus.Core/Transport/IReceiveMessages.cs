using System;
namespace CWServiceBus.Transport {
    public interface IReceiveMessages {
        ITransportMessageSerializer TransportMessageSerializer { get; }
        void Init();
        TransportMessage Receive(ITransactionToken transactionToken, Action onAfterWaitingCallback);
    }
}
