using System;
using System.Reflection;
using CWServiceBus;
using CWServiceBus.ServiceBroker;
using CWServiceBus.StructureMap;
using MyMessages;
using StructureMap;

namespace MyPublisher {
    public class Program {
        static IServiceBus ServiceBus;

        public static void Main() {
            log4net.Config.XmlConfigurator.Configure();

            var container = new Container(i => {
            });

            var serviceBus = ServiceBusBuilder.Initialize(builder => {
                builder.ServiceLocator = new StructureMapServiceLocator(container);
                builder.MessageTypeConventions.AddConvention(t => t.Namespace == "MyMessages");
                builder.AddAssembliesToScan(Assembly.Load("MyMessages"));
                builder.UseServiceBrokerTransport(t => {
                    t.ListenerQueue = "CWServiceBus_Samples_PubSub_Publisher";
                    t.ReturnAddress = "[//CWServiceBus/Samples/PubSub/Publisher]";
                    t.NumberOfWorkerThreads = 1;
                    t.ServiceBrokerConnectionString = "Data Source=localhost;Initial Catalog=ServiceBus;Trusted_Connection=true";
                });
            });

            serviceBus.Start();
            ServiceBus = serviceBus;

            Run();
        }

        public static void Run() {
            Console.WriteLine("This will publish IEvent and EventMessage alternately.");
            Console.WriteLine("Press 'Enter' to publish a message.To exit, Ctrl + C");

            bool publishIEvent = true;
            while (Console.ReadLine() != null) {
                var eventMessage = publishIEvent ? ServiceBus.CreateInstance<IMyEvent>() : new EventMessage();

                eventMessage.EventId = Guid.NewGuid();
                eventMessage.Time = DateTime.Now.Second > 30 ? (DateTime?)DateTime.Now : null;
                eventMessage.Duration = TimeSpan.FromSeconds(99999D);

                ServiceBus.Publish(eventMessage);

                Console.WriteLine("Published event with Id {0}.", eventMessage.EventId);
                Console.WriteLine("==========================================================================");

                publishIEvent = !publishIEvent;
            }
        }
    }
}
