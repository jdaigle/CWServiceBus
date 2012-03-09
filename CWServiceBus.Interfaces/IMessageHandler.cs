namespace CWServiceBus
{
    public interface IMessageHandler<TMessage>
    {
        void Handle(TMessage message);
    }
}
