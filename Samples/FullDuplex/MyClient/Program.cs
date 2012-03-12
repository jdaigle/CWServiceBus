using System;
using System.Diagnostics;
using System.Reflection;
using CWServiceBus;
using CWServiceBus.ServiceBroker;
using CWServiceBus.StructureMap;
using CWServiceBus.Unicast;
using MyMessages;
using StructureMap;

namespace MyClient {
    public class Program {

        static IMessageBus messageBus;

        public static void Main() {
            log4net.Config.XmlConfigurator.Configure();

            var container = new Container(i => {
            });

            messageBus = MessageBusBuilder.Initialize(builder => {
                builder.ServiceLocator = new StructureMapServiceLocator(container);
                builder.MessageTypeConventions.AddConvention(t => t.Namespace == "MyMessages");
                builder.AddAssembliesToScan(Assembly.Load("MyMessages"));
                builder.AddAssembliesToScan(Assembly.Load("MyClient"));
                builder.MessageEndpointMappingCollection.Add(new CWServiceBus.Config.MessageEndpointMapping() {
                    Messages = "MyMessages",
                    Endpoint = "[//CWServiceBus/Samples/FullDuplex/Server]",
                });
                builder.UseServiceBrokerTransport(t => {
                    t.ListenerQueue = "CWServiceBus_Samples_FullDuplex_Client";
                    t.ReturnAddress = "[//CWServiceBus/Samples/FullDuplex/Client]";
                    t.NumberOfWorkerThreads = 1;
                    t.ServiceBrokerConnectionString = "Data Source=localhost;Initial Catalog=ServiceBus;Trusted_Connection=true";
                });
            });

            ((IStartableMessageBus)messageBus).Start();

            Run();
        }


        public static void Run() {
            Console.WriteLine("Press 'Enter' to send a message.To exit, Ctrl + C");

            while (Console.ReadLine() != null) {
                Guid g = Guid.NewGuid();

                Console.WriteLine("==========================================================================");
                Console.WriteLine("Requesting to get data by id: {0}", g.ToString("N"));

                messageBus.SetHeader("Test", "N");

                var watch = new Stopwatch();
                watch.Start();
                messageBus.Send<RequestDataMessage>(m => {
                    m.DataId = g;
                    m.String = "<node>it's my \"node\" & i like it<node>";
                });

                watch.Stop();

                Console.WriteLine("Elapsed time: {0}", watch.ElapsedMilliseconds);
            }
        }
    }
}
