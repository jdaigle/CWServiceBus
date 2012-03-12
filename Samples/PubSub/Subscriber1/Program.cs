using System.Reflection;
using System.Threading;
using CWServiceBus;
using CWServiceBus.ServiceBroker;
using CWServiceBus.StructureMap;
using MyMessages;
using StructureMap;

namespace Subscriber1 {
    public class Program {

        public static void Main() {
            log4net.Config.XmlConfigurator.Configure();

            var container = new Container(i => {
            });

            var messageBus = MessageBusBuilder.Initialize(builder => {
                builder.ServiceLocator = new StructureMapServiceLocator(container);
                builder.MessageTypeConventions.AddConvention(t => t.Namespace == "MyMessages");
                builder.AddAssembliesToScan(Assembly.Load("MyMessages"));
                builder.AddAssembliesToScan(Assembly.Load("Subscriber1"));
                builder.MessageEndpointMappingCollection.Add(new CWServiceBus.Config.MessageEndpointMapping() {
                    Messages = "MyMessages",
                    Endpoint = "[//CWServiceBus/Samples/PubSub/Publisher]",
                });
                builder.UseServiceBrokerTransport(t => {
                    t.ListenerQueue = "CWServiceBus_Samples_PubSub_Subscriber1";
                    t.ReturnAddress = "[//CWServiceBus/Samples/PubSub/Subscriber1]";
                    t.NumberOfWorkerThreads = 1;
                    t.ServiceBrokerConnectionString = "Data Source=localhost;Initial Catalog=ServiceBus;Trusted_Connection=true";
                });
            });

            messageBus.Start();

            messageBus.Subscribe<EventMessage>();

            while (true)
                Thread.Sleep(5000);
        }
    }
}
