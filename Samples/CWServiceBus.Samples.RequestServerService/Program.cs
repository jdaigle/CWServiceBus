using System;
using System.Reflection;
using System.Threading;
using CWServiceBus.Samples.Messages;
using CWServiceBus.ServiceBroker;
using CWServiceBus.StructureMap;
using StructureMap;
using CWServiceBus.Dispatch;

namespace CWServiceBus.Samples.RequestServerService {
    public class Program {
        public static void Main() {
            log4net.Config.XmlConfigurator.Configure();

            var container = new Container(i => {
    //            i.ForSingletonOf<IDispatchInspector>().Use<UnitOfWorkDispatchInspector>();
            });            

            var serviceBus = ServiceBusBuilder.Initialize(builder => {
                builder.ServiceLocator = new StructureMapServiceLocator(container);
                builder.MessageTypeConventions.AddConvention(t => t.Namespace == "CWServiceBus.Samples.Messages");
                builder.AddAssembliesToScan(Assembly.Load("CWServiceBus.Samples.RequestServerService"));
                builder.UseServiceBrokerTransport(t => {
                    t.ListenerQueue = "CWServiceBusTestQueue";
                    t.ReturnAddress = "[//CWServiceBus/Test]";
                    t.NumberOfWorkerThreads = 1;
                    t.ServiceBrokerConnectionString = "Data Source=localhost;Initial Catalog=ServiceBus;Trusted_Connection=true";
                });
            });

            serviceBus.Start();

            while (true) {
                Thread.Sleep(5000);
                serviceBus.Send<MyRequest>("[//CWServiceBus/Test]", x => {
                    x.MyRequestId = Guid.NewGuid();
                    x.MyRequestData = Guid.NewGuid().ToString();
                });
            }
        }
    }

    public class UnitOfWorkDispatchInspector : IDispatchInspector {
        public void OnDispatching(IServiceLocator childServiceLocator, IMessageContext messageContext) {
            return;
        }

        public void OnDispatched(IServiceLocator childServiceLocator, IMessageContext messageContext, bool withError) {
            return;
        }

        public void OnDispatchException(IServiceLocator childServiceLocator, IMessageContext messageContext, Exception exception) {
            return;
        }
    }


    public class TestHandler : IMessageHandler<MyRequest> {
        public void Handle(MyRequest message) {
            Thread.Sleep(1000);
        }
    }

}
