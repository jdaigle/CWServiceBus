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
        private IEnumerable<IDispatchInspector> dispatchInspectors;
        private MessageHandlerCollection messageHandlers;
        private static readonly ILog Logger = LogManager.GetLogger(typeof(MessageDispatcher).Namespace);

        public void DispatchMessages(IEnumerable<object> messages, IMessageContext messageContext) {
            using (var childServiceLocator = serviceLocator.GetChildServiceLocator()) {
                Exception exception = null;
                try {
                    OnDispatching(childServiceLocator, messages, messageContext);
                    foreach (var message in messages) {
                        var messageType = message.GetType();
                        foreach (var messageHandler in messageHandlers.GetOrderedHandlersFor(messageType)) {
                            var handler = childServiceLocator.Get(messageHandler.InstanceType);
                            messageHandler.MethodInfo.Invoke(handler, new[] { message });
                        }
                    }
                } catch (Exception e) {
                    Logger.Warn("Failed Dispatching Messages for message with ID=" + messageContext.MessageId, e);
                    exception = e;
                    OnDispatchException(childServiceLocator, messages, messageContext, e);
                    throw;
                } finally {
                    OnDispatched(childServiceLocator, messages, messageContext, exception != null);
                }
            }
        }

        private void OnDispatching(IServiceLocator childServiceLocator, IEnumerable<object> messages, IMessageContext messageContext) {
            if (dispatchInspectors == null) dispatchInspectors = serviceLocator.GetAll<IDispatchInspector>();
            if (dispatchInspectors != null)
                foreach (var dispatchInspector in dispatchInspectors)
                    dispatchInspector.OnDispatching(childServiceLocator, messageContext);
            if (Dispatching != null) {
                Dispatching(this, new MessageDispatcherEventArgs() {
                    Messages = messages,
                });
            }
        }

        private void OnDispatched(IServiceLocator childServiceLocator, IEnumerable<object> messages, IMessageContext messageContext, bool withError) {
            if (dispatchInspectors == null) dispatchInspectors = serviceLocator.GetAll<IDispatchInspector>();
            if (dispatchInspectors != null)
                foreach (var dispatchInspector in dispatchInspectors)
                    dispatchInspector.OnDispatched(childServiceLocator, messageContext, withError);
            if (Dispatched != null) {
                Dispatched(this, new MessageDispatcherEventArgs() {
                    Messages = messages,
                    DispatchedWithError = withError
                });
            }
        }

        private void OnDispatchException(IServiceLocator childServiceLocator, IEnumerable<object> messages, IMessageContext messageContext, Exception e) {
            if (dispatchInspectors == null) dispatchInspectors = serviceLocator.GetAll<IDispatchInspector>();
            if (dispatchInspectors != null)
                foreach (var dispatchInspector in dispatchInspectors)
                    dispatchInspector.OnDispatchException(childServiceLocator, messageContext, e);
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
