using CWServiceBus.Config;
using CWServiceBus.Serializers;
using CWServiceBus.ServiceBroker.Transport;
using CWServiceBus.Transport;
using System.Collections.Generic;

namespace CWServiceBus.SqlServer
{
    public class SqlServerTransportBuilder : ITransportBuilder {

        public SqlServerTransportBuilder(MessageBusBuilder messageBusBuilder)
        {
            this.MessageBusBuilder = messageBusBuilder;
            this.MessageBusBuilder.MessageTypeConventions.AddConvention(t => t == typeof(HeaderInfo));
        }

        public MessageBusBuilder MessageBusBuilder { get; private set; }

        public string ListenerQueue { get; set; }
        public string ConnectionString { get; set; }

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

        private HashSet<string> faultForwardDestinations = new HashSet<string>();
        public void ForwardFaultsTo(string destination) {
            faultForwardDestinations.Add(destination);
        }

        public ITransport Build() {
            var transport = new SqlServerTransport(ListenerQueue, new SqlServerTransactionWrapper(ConnectionString), MessageBusBuilder.MessageSerializer, NumberOfWorkerThreads);
            transport.MaxRetries = this.MaxRetries;
            transport.ForwardFaultsTo(faultForwardDestinations);
            return transport;
        }


        public string EndpointName { get { return ListenerQueue; } }
    }
}
