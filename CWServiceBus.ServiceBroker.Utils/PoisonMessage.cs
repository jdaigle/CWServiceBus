namespace CWServiceBus.ServiceBroker.Utils
{
    public class PoisonMessage
    {
        public PoisonMessageInfo Info { get; set; }
        public byte[] UTF8EncodedMessage { get; set; }
        public string ExceptionMessage { get; set; }
    }
}
