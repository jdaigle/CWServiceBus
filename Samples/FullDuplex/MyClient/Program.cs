using System.Reflection;
using System.Threading;
using CWServiceBus;
using CWServiceBus.ServiceBroker;
using CWServiceBus.StructureMap;
using StructureMap;
using System;
using System.Diagnostics;
using MyMessages;
using CWServiceBus.Unicast;

namespace MyClient {
    public class Program {

        static IServiceBus serviceBus;

        public static void Main() {
            log4net.Config.XmlConfigurator.Configure();

            var container = new Container(i => {
            });

            serviceBus = ServiceBusBuilder.Initialize(builder => {
                builder.ServiceLocator = new StructureMapServiceLocator(container);
                builder.MessageTypeConventions.AddConvention(t => t.Namespace == "MyMessages");
                builder.AddAssembliesToScan(Assembly.Load("MyMessages"));
                builder.AddAssembliesToScan(Assembly.Load("MyClient"));
                builder.UseServiceBrokerTransport(t => {
                    t.ListenerQueue = "CWServiceBus_Samples_FullDuplex_Client";
                    t.ReturnAddress = "[//CWServiceBus/Samples/FullDuplex/Client]";
                    t.NumberOfWorkerThreads = 1;
                    t.ServiceBrokerConnectionString = "Data Source=localhost;Initial Catalog=ServiceBus;Trusted_Connection=true";
                });
            });

            ((IStartableServiceBus)serviceBus).Start();

            Run();
        }


        public static void Run() {
            Console.WriteLine("Press 'Enter' to send a message.To exit, Ctrl + C");

            while (Console.ReadLine() != null) {
                Guid g = Guid.NewGuid();

                Console.WriteLine("==========================================================================");
                Console.WriteLine("Requesting to get data by id: {0}", g.ToString("N"));

                //serviceBus.OutgoingHeaders["Test"] = g.ToString("N");

                var watch = new Stopwatch();
                watch.Start();
                serviceBus.Send<RequestDataMessage>("[//CWServiceBus/Samples/FullDuplex/Server]", m => {
                    m.DataId = g;
                    m.String = "<node>it's my \"node\" & i like it<node>";
                })
                    //.Register<int>(i => {
                    //        Console.WriteLine("==========================================================================");
                    //        Console.WriteLine(
                    //            "Response with header 'Test' = {0}, 1 = {1}, 2 = {2}.",
                    //            serviceBus.CurrentMessageContext.Headers["Test"],
                    //            serviceBus.CurrentMessageContext.Headers["1"],
                    //            serviceBus.CurrentMessageContext.Headers["2"]);
                    //    });
                    ;

                watch.Stop();

                Console.WriteLine("Elapsed time: {0}", watch.ElapsedMilliseconds);
            }
        }
    }
}
