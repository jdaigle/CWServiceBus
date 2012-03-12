using CWServiceBus.Config;
using CWServiceBus.Faults;
using CWServiceBus.Serializers;
using CWServiceBus.ServiceBroker.Transport;
using CWServiceBus.Transport;

namespace CWServiceBus.ServiceBroker.Config {
    public class ServiceBrokerTransportBuilder : ITransportBuilder {

        public ServiceBrokerTransportBuilder(MessageBusBuilder messageBusBuilder) {
            this.MessageBusBuilder = messageBusBuilder;
        }

        public MessageBusBuilder MessageBusBuilder { get; private set; }

        public string ListenerQueue { get; set; }
        public string ReturnAddress { get; set; }
        public string ServiceBrokerConnectionString { get; set; }

        private int maxRetries = 5;
        public int MaxRetries {
            get { return maxRetries; }
            set { maxRetries = value; }
        }

        private int numberOfWorkerThreads = 5;
        public int NumberOfWorkerThreads {
            get { return numberOfWorkerThreads; }
            set { numberOfWorkerThreads = value; }
        }

        public ITransport Build() {
            var transportMessageSerializer = new XmlTransportMessageSerializer(MessageBusBuilder.MessageSerializer);
            IManageMessageFailures failureManager = null;
            var transport = new ServiceBrokerTransport(ListenerQueue, ReturnAddress, new SqlServerTransactionWrapper(ServiceBrokerConnectionString), transportMessageSerializer, failureManager, NumberOfWorkerThreads);
            transport.MaxRetries = this.MaxRetries;
            return transport;
        }
    }
}
