namespace CWServiceBus.ServiceBroker.Utils
{
    public class PoisonMessage
    {
        public PoisonMessageInfo Info { get; set; }
        public string Message { get; set; }
        public string ExceptionMessage { get; set; }
    }
}
