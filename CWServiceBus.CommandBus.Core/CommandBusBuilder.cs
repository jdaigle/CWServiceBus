using System;
using System.Collections.Generic;
using System.Reflection;
using CWServiceBus.CommandBus.Unicast;
using CWServiceBus.Dispatch;
using CWServiceBus.Reflection;
using log4net;

namespace CWServiceBus.CommandBus {
    public class CommandBusBuilder {

        private static ILog Logger = log4net.LogManager.GetLogger(typeof(CommandBusBuilder));

        public static ICommandBus Initialize(Action<CommandBusBuilder> intialize) {
            var builder = new CommandBusBuilder();
            intialize(builder);
            return builder.Build();
        }

        public CommandBusBuilder() {
            this.MessageTypeConventions = new MessageTypeConventions();
        }

        private ICommandBus Build() {
            var messageTypes = MessageTypeConventions.ScanAssembliesForMessageTypes(assembliesToScan);
            var messageMapper = new MessageMapper();
            messageMapper.SetMessageTypeConventions(this.MessageTypeConventions);
            messageMapper.Initialize(messageTypes);

            var messageHandlers = new MessageHandlerCollection(this.MessageTypeConventions);
            messageHandlers.AddAssembliesToScan(assembliesToScan);
            messageHandlers.Init();

            var messageDispatcher = new MessageDispatcher(ServiceLocator, messageHandlers);
            var commandBus = new UnicastCommandBus(messageMapper, messageDispatcher);
            return commandBus;
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

        public IServiceLocator ServiceLocator { get; set; }
        public MessageTypeConventions MessageTypeConventions { get; private set; }
    }
}
