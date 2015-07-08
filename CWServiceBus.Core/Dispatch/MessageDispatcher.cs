using System;
using System.Collections.Generic;
using System.Reflection;
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
                    foreach (var messageHandlerDispatchInfo in messageHandlers.GetOrderedDispatchInfoFor(messageType))
                    {
                        var handler = childServiceLocator.Get(messageHandlerDispatchInfo.InstanceType);
                        var tryDisposeHandler = false;
                        if (handler == null)
                        {
                            handler = Activator.CreateInstance(messageHandlerDispatchInfo.InstanceType);
                            tryDisposeHandler = true;
                        }
                        childServiceLocator.BuildUp(handler);
                        try
                        {
                            Logger.DebugFormat("Dispatching message {0} to handler {1}", messageType, handler);
                            messageHandlerDispatchInfo.Invoke(handler, message);
                        }
                        finally
                        {
                            if (tryDisposeHandler && handler is IDisposable)
                            {
                                ((IDisposable)handler).Dispose();
                            }
                        }
                    }
                }
            } catch (Exception e) {
                exception = e;
                if (exception is TargetInvocationException && exception.InnerException != null) {
                    exception = e.InnerException;
                }
                Logger.Warn("Failed Dispatching Messages for message with ID=" + messageContext.MessageId, exception);
                OnDispatchException(childServiceLocator, messages, messageContext, exception);
                throw;
            } finally {
                OnDispatched(childServiceLocator, messages, messageContext, exception);
            }
        }

        private void OnDispatching(IServiceLocator childServiceLocator, IEnumerable<object> messages, IMessageContext messageContext) {
            EnsureUnitOfWorkManagersLoaded();
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
            EnsureUnitOfWorkManagersLoaded();
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

        private void EnsureUnitOfWorkManagersLoaded()
        {
            if (unitOfWorkManagers == null)
            {
                lock (serviceLocator)
                {
                    if (unitOfWorkManagers == null)
                    {
                        unitOfWorkManagers = serviceLocator.GetAll<IManagesUnitOfWork>();
                    }
                }
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
