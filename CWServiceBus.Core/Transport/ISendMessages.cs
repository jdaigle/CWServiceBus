namespace CWServiceBus.Transport {
    public interface ISendMessages {
        void Send(TransportMessage message, string destination);
    }
}