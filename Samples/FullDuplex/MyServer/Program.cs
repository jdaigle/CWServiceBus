using System.Reflection;
using System.Threading;
using CWServiceBus;
using CWServiceBus.ServiceBroker;
using CWServiceBus.StructureMap;
using StructureMap;

namespace MyServer {
    public class Program {
        public static void Main() {
            log4net.Config.XmlConfigurator.Configure();

            var container = new Container(i => {
                i.ForSingletonOf<IManagesUnitOfWork>().Use<MyOwnUnitOfWork>();
            });

            var serviceBus = ServiceBusBuilder.Initialize(builder => {
                builder.ServiceLocator = new StructureMapServiceLocator(container);
                builder.MessageTypeConventions.AddConvention(t => t.Namespace == "MyMessages");
                builder.AddAssembliesToScan(Assembly.Load("MyMessages"));
                builder.AddAssembliesToScan(Assembly.Load("MyServer"));
                builder.UseServiceBrokerTransport(t => {
                    t.ListenerQueue = "CWServiceBus_Samples_FullDuplex_Server";
                    t.ReturnAddress = "[//CWServiceBus/Samples/FullDuplex/Server]";
                    t.NumberOfWorkerThreads = 1;
                    t.ServiceBrokerConnectionString = "Data Source=localhost;Initial Catalog=ServiceBus;Trusted_Connection=true";
                });
            });

            serviceBus.Start();

            while (true)
                Thread.Sleep(5000);
        }
    }
}
