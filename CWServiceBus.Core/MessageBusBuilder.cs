using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using CWServiceBus.Config;
using CWServiceBus.Dispatch;
using CWServiceBus.Reflection;
using CWServiceBus.Serializers.XML;
using CWServiceBus.Unicast;
using log4net;
using CWServiceBus.Diagnostics;

namespace CWServiceBus {
    public class MessageBusBuilder {

        private static ILog Logger = log4net.LogManager.GetLogger(typeof(MessageBusBuilder));

        public static IStartableMessageBus Initialize(Action<MessageBusBuilder> intialize) {
            var builder = new MessageBusBuilder();
            intialize(builder);
            return builder.Build();
        }

        public MessageBusBuilder() {
            this.MessageTypeConventions = new MessageTypeConventions();
            this.MessageEndpointMappingCollection = new MessageEndpointMappingCollection();
            AddAssemblyToScan(Assembly.Load("CWServiceBus.Core"));
        }

        private IStartableMessageBus Build() {
            var messageTypes = MessageTypeConventions.ScanAssembliesForMessageTypes(assembliesToScan);
            var messageMapper = new MessageMapper();
            messageMapper.SetMessageTypeConventions(this.MessageTypeConventions);
            messageMapper.Initialize(messageTypes);
            var allMessageTypes = messageTypes.Concat(messageMapper.DynamicTypes);

            MessageSerializer = new XmlMessageSerializer(messageMapper);
            (MessageSerializer as XmlMessageSerializer).Initialize(messageTypes);

            var messageHandlers = new MessageHandlerCollection(this.MessageTypeConventions);
            messageHandlers.AddAssembliesToScan(assembliesToScan);
            messageHandlers.Init();
            if (executeTheseHandlersFirst.Any())
                messageHandlers.ExecuteTheseHandlersFirst(executeTheseHandlersFirst);
            if (executeTheseHandlersLast.Any())
                messageHandlers.ExecuteTheseHandlersLast(executeTheseHandlersLast);

            // Get endpoint mapping
            foreach (MessageEndpointMapping mapping in this.MessageEndpointMappingCollection) {
                try {
                    var messageType = Type.GetType(mapping.Messages, false);
                    if (messageType != null && MessageTypeConventions.IsMessageType(messageType))
                    {
                        typesToEndpoints[messageType] = mapping.Endpoint.Trim();
                        continue;
                    }
                } catch (Exception ex) {
                    Logger.Error("Problem loading message type: " + mapping.Messages, ex);
                }

                try {
                    var a = Assembly.Load(mapping.Messages);
                    foreach (var t in a.GetTypes().Where(t => MessageTypeConventions.IsMessageType(t)))
                        typesToEndpoints[t] = mapping.Endpoint.Trim();
                } catch (Exception ex) {
                    throw new ArgumentException("Problem loading message assembly: " + mapping.Messages, ex);
                }

            }

            var transport = TransportBuilder.Build();

            var messageDispatcher = new MessageDispatcher(ServiceLocator, messageHandlers);
            var messageBus = new UnicastMessageBus(messageMapper, transport, messageDispatcher, SubscriptionStorage);
            messageBus.MapMessageTypesToAddress(typesToEndpoints);

            if (DiagnosticsPerfCountersEnabled)
            {
                var performanceCounters = new PerformanceCounters(TransportBuilder.EndpointName);
                messageBus.MessageReceived += (o, e) => performanceCounters.OnMessageReceived();
                messageBus.MessageSent += (o, e) => performanceCounters.OnMessageSent();
                messageBus.MessageFailed += (o, e) => performanceCounters.OnMessageFailure();
                messageBus.MessageHandled += (o, e) => performanceCounters.OnMessageHandled(e.ElapsedMilliseconds, e.ElapsedTicks);
            }

            return messageBus;
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

        private Type[] executeTheseHandlersFirst = new Type[0];
        public void ExecuteTheseHandlersFirst(params Type[] handlerTypes) {
            this.executeTheseHandlersFirst = handlerTypes;
        }

        private Type[] executeTheseHandlersLast = new Type[0];
        public void ExecuteTheseHandlersLast(params Type[] handlerTypes) {
            this.executeTheseHandlersLast = handlerTypes;
        }

        /// <summary>
        /// Maps all messages from an assembly, or a single message type to a particular endpoint for sending.
        /// </summary>
        /// <param name="message">A string defining the message assembly, or single message type.</param>
        /// <param name="endpoint">The endpoint named</param>
        public void MapMessageEndpoint(string message, string endpoint)
        {
            this.MessageEndpointMappingCollection.Add(new MessageEndpointMapping()
            {
                Messages = message,
                Endpoint = endpoint,
            });
        }

        private readonly IDictionary<Type, string> typesToEndpoints = new Dictionary<Type, string>();
        public MessageEndpointMappingCollection MessageEndpointMappingCollection { get; private set; }
        public IServiceLocator ServiceLocator { get; set; }
        public MessageTypeConventions MessageTypeConventions { get; private set; }
        public IMessageSerializer MessageSerializer { get; private set; }
        public ITransportBuilder TransportBuilder { get; set; }
        public ISubscriptionStorage SubscriptionStorage { get; set; }
        public bool DiagnosticsPerfCountersEnabled { get; set; }
    }
}
