using System;
using System.Collections.Generic;
using log4net;

namespace CWServiceBus.Dispatch {
    public class MessageDispatcher : IMessageDispatcher {

        public MessageDispatcher(IServiceLocator serviceLocator, MessageHandlerCollection messageHandlers) {
            this.serviceLocator = serviceLocator;
            this.messageHandlers = messageHandlers;
        }

        private IServiceLocator serviceLocator;
        private IEnumerable<IManagesUnitOfWork> unitOfWorkManagers;
        private MessageHandlerCollection messageHandlers;
        private static readonly ILog Logger = LogManager.GetLogger(typeof(MessageDispatcher).Namespace);

        public IServiceLocator ServiceLocator { get { return serviceLocator; } }

        public void DispatchMessages(IServiceLocator childServiceLocator, IEnumerable<object> messages, IMessageContext messageContext) {
            Exception exception = null;
            try {
                OnDispatching(childServiceLocator, messages, messageContext);
                foreach (var message in messages) {
                    var messageType = message.GetType();
                    foreach (var messageHandlerDispatchInfo in messageHandlers.GetOrderedDispatchInfoFor(messageType)) {
                        var handler = childServiceLocator.Get(messageHandlerDispatchInfo.InstanceType);
                        messageHandlerDispatchInfo.Invoke(handler, message);
                    }
                }
            } catch (Exception e) {
                Logger.Warn("Failed Dispatching Messages for message with ID=" + messageContext.MessageId, e);
                exception = e;
                OnDispatchException(childServiceLocator, messages, messageContext, e);
                throw;
            } finally {
                OnDispatched(childServiceLocator, messages, messageContext, exception);
            }
        }

        private void OnDispatching(IServiceLocator childServiceLocator, IEnumerable<object> messages, IMessageContext messageContext) {
            if (unitOfWorkManagers == null) unitOfWorkManagers = serviceLocator.GetAll<IManagesUnitOfWork>();
            if (unitOfWorkManagers != null)
                foreach (var unitOfWorkManager in unitOfWorkManagers)
                    unitOfWorkManager.Begin(childServiceLocator, messageContext);
            if (Dispatching != null) {
                Dispatching(this, new MessageDispatcherEventArgs() {
                    Messages = messages,
                });
            }
        }

        private void OnDispatched(IServiceLocator childServiceLocator, IEnumerable<object> messages, IMessageContext messageContext, Exception e) {
            if (unitOfWorkManagers == null) unitOfWorkManagers = serviceLocator.GetAll<IManagesUnitOfWork>();
            if (unitOfWorkManagers != null)
                foreach (var unitOfWorkManager in unitOfWorkManagers)
                    unitOfWorkManager.End(childServiceLocator, messageContext, e);
            if (Dispatched != null) {
                Dispatched(this, new MessageDispatcherEventArgs() {
                    Messages = messages,
                    DispatchedWithError = e != null,
                });
            }
        }

        private void OnDispatchException(IServiceLocator childServiceLocator, IEnumerable<object> messages, IMessageContext messageContext, Exception e) {
            if (DispatchException != null) {
                DispatchException(this, new MessageDispatcherEventArgs() {
                    Messages = messages,
                    DispatchException = e,
                });
            }
        }


        public event EventHandler<MessageDispatcherEventArgs> Dispatching;
        public event EventHandler<MessageDispatcherEventArgs> Dispatched;
        public event EventHandler<MessageDispatcherEventArgs> DispatchException;
    }
}
