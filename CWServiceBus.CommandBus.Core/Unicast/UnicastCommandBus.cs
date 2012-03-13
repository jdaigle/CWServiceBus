using System;
using System.Linq;
using log4net;

namespace CWServiceBus.CommandBus.Unicast {
    public class UnicastCommandBus : ICommandBus {

        private readonly static ILog Logger = LogManager.GetLogger(typeof(UnicastCommandBus));
        private IMessageMapper messageMapper;
        private IMessageDispatcher messageDispatcher;

        public UnicastCommandBus() { }

        public UnicastCommandBus(IMessageMapper messageMapper, IMessageDispatcher messageDispatcher) {
            this.messageMapper = messageMapper;
            this.messageDispatcher = messageDispatcher;
        }

        public void Send(params object[] commands) {
            if (!commands.Any())
                return;
            using (var childServiceLocator = this.messageDispatcher.ServiceLocator.GetChildServiceLocator()) {
                this.messageDispatcher.DispatchMessages(childServiceLocator, commands, new MessageContext());
            }
        }

        public void Send<T>(Action<T> commandConstructor) {
            Send(CreateInstance(commandConstructor));
        }

        public T CreateInstance<T>() {
            return messageMapper.CreateInstance<T>();
        }

        public T CreateInstance<T>(Action<T> action) {
            return messageMapper.CreateInstance(action);
        }

        public object CreateInstance(Type messageType) {
            return messageMapper.CreateInstance(messageType);
        }
    }
}
