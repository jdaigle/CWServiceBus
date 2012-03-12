using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CWServiceBus.Config;
using CWServiceBus.Dispatch;
using CWServiceBus.Reflection;
using CWServiceBus.Serializers.XML;
using CWServiceBus.Unicast;

namespace CWServiceBus {
    public class ServiceBusBuilder {
        public static IStartableServiceBus Initialize(Action<ServiceBusBuilder> intialize) {
            var builder = new ServiceBusBuilder();
            intialize(builder);
            return builder.Build();
        }

        public ServiceBusBuilder() {
            this.MessageTypeConventions = new MessageTypeConventions();
            messageHandlers = new MessageHandlerCollection(this.MessageTypeConventions);
        }

        private IStartableServiceBus Build() {
            messageHandlers.AddAssembliesToScan(assembliesToScan);
            messageHandlers.Init();

            var messageTypes = MessageTypeConventions.ScanAssembliesForMessageTypes(assembliesToScan);
            var messageMapper = new MessageMapper();
            messageMapper.SetMessageTypeConventions(this.MessageTypeConventions);
            messageMapper.Initialize(messageTypes);

            MessageSerializer = new XmlMessageSerializer(messageMapper);
            (MessageSerializer as XmlMessageSerializer).Initialize(messageTypes);

            var transport = TransportBuilder.Build();

            var messageDispatcher = new MessageDispatcher(ServiceLocator, messageHandlers);
            var serviceBus = new UnicastServiceBus(messageMapper, transport, messageDispatcher, null);
            return serviceBus;
        }

        private ISet<Assembly> assembliesToScan = new HashSet<Assembly>();

        public void AddAssembliesToScan(IEnumerable<Assembly> assemblies) {
            foreach (var assembly in assemblies) {
                assembliesToScan.Add(assembly);
            }
        }

        public void AddAssembliesToScan(params Assembly[] assemblies) {
            AddAssembliesToScan((IEnumerable<Assembly>)assemblies);
        }

        public void AddAssemblyToScan(Assembly assembly) {
            AddAssembliesToScan(new[] { assembly });
        }

        private MessageHandlerCollection messageHandlers;
        public IServiceLocator ServiceLocator { get; set; }
        public MessageTypeConventions MessageTypeConventions { get; private set; }
        public IMessageSerializer MessageSerializer { get; private set; }
        public ITransportBuilder TransportBuilder { get; set; }
    }
}
