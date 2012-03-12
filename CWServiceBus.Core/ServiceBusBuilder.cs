using System;
using System.Collections.Generic;
using System.Reflection;
using CWServiceBus.Config;
using CWServiceBus.Dispatch;
using CWServiceBus.Reflection;
using CWServiceBus.Serializers.XML;
using CWServiceBus.Unicast;
using log4net;

namespace CWServiceBus {
    public class ServiceBusBuilder {

        private static ILog Logger = log4net.LogManager.GetLogger(typeof(ServiceBusBuilder));

        public static IStartableServiceBus Initialize(Action<ServiceBusBuilder> intialize) {
            var builder = new ServiceBusBuilder();
            intialize(builder);
            return builder.Build();
        }

        public ServiceBusBuilder() {
            this.MessageTypeConventions = new MessageTypeConventions();
            messageHandlers = new MessageHandlerCollection(this.MessageTypeConventions);
            this.MessageEndpointMappingCollection = new MessageEndpointMappingCollection();
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

            // Get endpoint mapping
            foreach (MessageEndpointMapping mapping in this.MessageEndpointMappingCollection) {
                try {
                    var messageType = Type.GetType(mapping.Messages, false);
                    if (messageType != null) {
                        typesToEndpoints[messageType] = mapping.Endpoint.Trim();
                        continue;
                    }
                } catch (Exception ex) {
                    Logger.Error("Problem loading message type: " + mapping.Messages, ex);
                }

                try {
                    var a = Assembly.Load(mapping.Messages);
                    foreach (var t in a.GetTypes())
                        typesToEndpoints[t] = mapping.Endpoint.Trim();
                } catch (Exception ex) {
                    throw new ArgumentException("Problem loading message assembly: " + mapping.Messages, ex);
                }

            }

            var transport = TransportBuilder.Build();

            var messageDispatcher = new MessageDispatcher(ServiceLocator, messageHandlers);
            var serviceBus = new UnicastServiceBus(messageMapper, transport, messageDispatcher, null);
            serviceBus.MapMessageTypesToAddress(typesToEndpoints);
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
        private readonly IDictionary<Type, string> typesToEndpoints = new Dictionary<Type, string>();
        public MessageEndpointMappingCollection MessageEndpointMappingCollection { get; private set; }
        public IServiceLocator ServiceLocator { get; set; }
        public MessageTypeConventions MessageTypeConventions { get; private set; }
        public IMessageSerializer MessageSerializer { get; private set; }
        public ITransportBuilder TransportBuilder { get; set; }
    }
}
