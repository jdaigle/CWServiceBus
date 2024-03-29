﻿using System.Reflection;
using System.Threading;
using CWServiceBus;
using CWServiceBus.ServiceBroker;
using CWServiceBus.StructureMap;
using MyMessages;
using StructureMap;

namespace Subscriber2 {
    public class Program {
        public static void Main() {
            log4net.Config.XmlConfigurator.Configure();

            var container = new Container(i => {
            });

            var messageBus = MessageBusBuilder.Initialize(builder => {
                builder.ServiceLocator = new StructureMapServiceLocator(container);
                builder.MessageTypeConventions.AddConvention(t => t.Namespace == "MyMessages");
                builder.AddAssembliesToScan(Assembly.Load("MyMessages"));
                builder.AddAssembliesToScan(Assembly.Load("Subscriber2"));
                builder.MessageEndpointMappingCollection.Add(new CWServiceBus.Config.MessageEndpointMapping() {
                    Messages = "MyMessages",
                    Endpoint = "[//CWServiceBus/Samples/PubSub/Publisher]",
                });
                builder.UseServiceBrokerTransport(t => {
                    t.ListenerQueue = "CWServiceBus_Samples_PubSub_Subscriber2";
                    t.ReturnAddress = "[//CWServiceBus/Samples/PubSub/Subscriber2]";
                    t.NumberOfWorkerThreads = 1;
                    t.ServiceBrokerConnectionString = "Data Source=localhost;Initial Catalog=ServiceBus;Trusted_Connection=true";
                });
            });

            messageBus.Start();
            messageBus.Subscribe<IMyEvent>();

            while (true)
                Thread.Sleep(5000);
        }
    }
}
