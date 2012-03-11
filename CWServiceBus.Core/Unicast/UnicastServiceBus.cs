using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CWServiceBus.Transport;
using log4net;

namespace CWServiceBus.Unicast {
    public class UnicastServiceBus : IServiceBus {

        private readonly static ILog Logger = LogManager.GetLogger(typeof(UnicastServiceBus).Namespace);
        private IMessageMapper messageMapper;
        private ISubscriptionStorage subscriptionStorage;
        private ISendMessages messageSender;

        /// <remarks>
        /// Accessed by multiple threads - needs appropriate locking
        /// </remarks>
        private readonly IDictionary<Type, string> messageTypeToDestinationLookup = new Dictionary<Type, string>();
        private readonly ReaderWriterLockSlim messageTypeToDestinationLocker = new ReaderWriterLockSlim();

        public void Publish<T>(params T[] messages) {

            if (subscriptionStorage == null)
                throw new InvalidOperationException("Cannot publish - no subscription storage has been configured.");

            if (messages == null || messages.Length == 0) {
                // Redirect (for exampple Bus.Publish<IFoo>();)
                Publish(CreateInstance<T>(m => { }));
                return;
            }

            var fullTypes = GetFullTypes(messages as object[]);
            var subscribers = subscriptionStorage
                .GetSubscriberServicesForMessage(fullTypes.Select(t => new MessageType(t)))
                .ToList();

            SendMessage(subscribers, null, MessageIntentEnum.Publish, messages as object[]);
        }

        public void Publish<T>(Action<T> messageConstructor) {
            Publish(CreateInstance(messageConstructor));
        }

        public void Subscribe(Type messageType) {
            Subscribe(null, messageType);
        }

        public void Subscribe(string publishingService, Type messageType) {



            //Logger.Info("Subscribing to " + messageType.AssemblyQualifiedName + " at publisher queue " + destination);
        }

        public void Subscribe<T>() {
            Subscribe(typeof(T));
        }

        public void Subscribe<T>(string publishingService) {
            throw new NotImplementedException();
        }

        public void Subscribe(Type messageType, Predicate<object> condition) {
            throw new NotImplementedException();
        }

        public void Subscribe(string publishingService, Type messageType, Predicate<object> condition) {
            throw new NotImplementedException();
        }

        public void Subscribe<T>(Predicate<T> condition) {
            throw new NotImplementedException();
        }

        public void Subscribe<T>(string publishingService, Predicate<T> condition) {
            throw new NotImplementedException();
        }

        public void Unsubscribe(Type messageType) {
            throw new NotImplementedException();
        }

        public void Unsubscribe(string publishingService, Type messageType) {
            throw new NotImplementedException();
        }

        public void Unsubscribe<T>() {
            throw new NotImplementedException();
        }

        public void Unsubscribe<T>(string publishingService) {
            throw new NotImplementedException();
        }

        public void SendLocal(params object[] messages) {
            throw new NotImplementedException();
        }

        public void SendLocal<T>(Action<T> messageConstructor) {
            throw new NotImplementedException();
        }

        void IServiceBus.Send(params object[] messages) {
            var destination = GetDestinationServiceForMessages(messages);
            SendMessage(destination, null, MessageIntentEnum.Send, messages);
        }

        void IServiceBus.Send<T>(Action<T> messageConstructor) {
            ((IServiceBus)this).Send(CreateInstance(messageConstructor));
        }

        void IServiceBus.Send(string destinationService, params object[] messages) {
            SendMessage(destinationService, null, MessageIntentEnum.Send, messages);
        }

        void IServiceBus.Send<T>(string destinationService, Action<T> messageConstructor) {
            SendMessage(destinationService, null, MessageIntentEnum.Send, CreateInstance(messageConstructor));
        }

        void IServiceBus.Send(string destinationService, Guid correlationId, params object[] messages) {
            SendMessage(destinationService, correlationId, MessageIntentEnum.Send, messages);
        }

        void IServiceBus.Send<T>(string destinationService, Guid correlationId, Action<T> messageConstructor) {
            SendMessage(destinationService, correlationId, MessageIntentEnum.Send, CreateInstance(messageConstructor));
        }

        private void SendMessage(string destination, Guid? correlationId, MessageIntentEnum messageIntent, params object[] messages) {
            if (messages == null || messages.Length == 0)
                throw new InvalidOperationException("Cannot send an empty set of messages.");

            if (string.IsNullOrEmpty(destination))
                throw new InvalidOperationException(string.Format("No destination specified for message {0}. Message cannot be sent.", messages[0].GetType().FullName));

            SendMessage(new List<string> { destination }, correlationId, messageIntent, messages);
        }

        private void SendMessage(IEnumerable<string> destinations, Guid? correlationId, MessageIntentEnum messageIntent, params object[] messages) {
            if (destinations == null || !destinations.Any() || destinations.Any(x => string.IsNullOrEmpty(x))) {
                throw new InvalidOperationException("No destination specified for message(s): " +
                                                            string.Join(";", messages.Select(m => m.GetType())));
            }

            var toSend = new TransportMessage { CorrelationId = correlationId.HasValue ? correlationId.ToString() : null, MessageIntent = messageIntent };
            MapTransportMessageFor(messages, toSend);

            foreach (var destination in destinations) {
                messageSender.Send(toSend, destination);

                if (Logger.IsDebugEnabled)
                    Logger.Debug(string.Format("Sending message {0} with ID {1} to destination {2}.\n" +
                                               "ToString() of the message yields: {3}\n" +
                                               "Message headers:\n{4}",
                                               messages[0].GetType().AssemblyQualifiedName,
                                               toSend.Id,
                                               destination,
                                               messages[0],
                                               string.Join(", ", toSend.Headers.Select(h => h.Key + ":" + h.Value).ToArray())
                        ));
            }
        }

        private TransportMessage MapTransportMessageFor(object[] messages, TransportMessage toSend) {
            toSend.Body = messages;
            toSend.WindowsIdentityName = Thread.CurrentPrincipal.Identity.Name;
            toSend.Headers = new List<HeaderInfo>();
            var timeToBeReceived = TimeSpan.MaxValue;
            toSend.TimeToBeReceived = timeToBeReceived;
            return toSend;
        }

        public void HandleCurrentMessageLater() {
            throw new NotImplementedException();
        }

        public void ForwardCurrentMessageTo(string destination) {
            throw new NotImplementedException();
        }

        public void DoNotContinueDispatchingCurrentMessageToHandlers() {
            throw new NotImplementedException();
        }

        public IDictionary<string, string> OutgoingHeaders {
            get { throw new NotImplementedException(); }
        }

        public IMessageContext CurrentMessageContext {
            get { throw new NotImplementedException(); }
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

        private List<Type> GetFullTypes(IEnumerable<object> messages) {
            var types = new List<Type>();

            foreach (var m in messages) {
                var s = m.GetType();
                if (types.Contains(s))
                    continue;
                types.Add(s);
                foreach (var t in m.GetType().GetInterfaces())
                    if (messageMapper.IsMessageType(t))
                        if (!types.Contains(t))
                            types.Add(t);
            }

            return types;
        }

        private string GetDestinationServiceForMessages(object[] messages) {
            if (!messages.Any()) return null;
            return GetDestinationServiceForMessage(messages.First().GetType());
        }

        private string GetDestinationServiceForMessage(Type messageType) {
            string destination;

            messageTypeToDestinationLocker.EnterReadLock();
            var destinationFound = messageTypeToDestinationLookup.TryGetValue(messageType, out destination);
            messageTypeToDestinationLocker.ExitReadLock();

            if (!destinationFound)
                return null;

            //if (destination != Address.Undefined)
            //    return destination;


            if (messageMapper != null && !messageType.IsInterface) {
                var t = messageMapper.GetMappedTypeFor(messageType);
                if (t != null && t != messageType)
                    return GetDestinationServiceForMessage(t);
            }

            return destination;
        }
    }
}
