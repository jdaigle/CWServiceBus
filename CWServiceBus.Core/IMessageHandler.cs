namespace CWServiceBus
{
    public interface IMessageHandler<TMessage> : IMessageHandler
    {
        void Handle(TMessage message);
    }

    public interface IMessageHandler { }
}
