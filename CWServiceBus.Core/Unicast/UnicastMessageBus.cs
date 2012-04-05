using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using CWServiceBus.Transport;
using log4net;
using System.Diagnostics;

namespace CWServiceBus.Unicast
{
    public class UnicastMessageBus : IMessageBus, IStartableMessageBus
    {

        public const string SubscriptionMessageType = "SubscriptionMessageType";

        private readonly static ILog Logger = LogManager.GetLogger(typeof(UnicastMessageBus));
        private IMessageMapper messageMapper;
        private ISubscriptionStorage subscriptionStorage;
        private ITransport transport;
        private IList<ITransport> additionalListeners = new List<ITransport>();
        private IMessageDispatcher messageDispatcher;

        public UnicastMessageBus() { }

        public UnicastMessageBus(IMessageMapper messageMapper, ITransport transport, IMessageDispatcher messageDispatcher, ISubscriptionStorage subscriptionStorage)
        {
            this.messageMapper = messageMapper;
            this.Transport = transport;
            this.messageDispatcher = messageDispatcher;
            this.subscriptionStorage = subscriptionStorage;
        }

        public ITransport Transport
        {
            get { return transport; }
            set
            {
                if (transport != null)
                {
                    transport.TransportMessageReceived -= Transport_TransportMessageReceived;
                    transport.FailedMessageProcessing -= Transport_FailedMessageProcessing;
                }
                transport = value;
                if (transport != null)
                {
                    transport.TransportMessageReceived += Transport_TransportMessageReceived;
                    transport.FailedMessageProcessing += Transport_FailedMessageProcessing;
                }
            }
        }

        

        public void AddAdditionalITransport(ITransport transport)
        {
            if (transport != null)
            {
                additionalListeners.Add(transport);
                transport.TransportMessageReceived += Transport_TransportMessageReceived;
            }
        }

        /// <remarks>
        /// Accessed by multiple threads - needs appropriate locking
        /// </remarks>
        private readonly IDictionary<Type, string> messageTypeToDestinationLookup = new Dictionary<Type, string>();
        private readonly ReaderWriterLockSlim messageTypeToDestinationLocker = new ReaderWriterLockSlim();

        public void Publish<T>(params T[] messages)
        {

            if (subscriptionStorage == null)
                throw new InvalidOperationException("Cannot publish - no subscription storage has been configured.");

            if (messages == null || messages.Length == 0)
            {
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

        public void Publish<T>(Action<T> messageConstructor)
        {
            Publish(CreateInstance(messageConstructor));
        }

        public void Subscribe(Type messageType)
        {
            Subscribe(null, messageType);
        }

        public void Subscribe(string publishingService, Type messageType)
        {
            if (string.IsNullOrWhiteSpace(publishingService))
            {
                publishingService = GetDestinationServiceForMessage(messageType);
            }
            Logger.Info("Subscribing to " + messageType.AssemblyQualifiedName + " at publisher " + publishingService);
            this.SetHeader(SubscriptionMessageType, messageType.AssemblyQualifiedName);
            SendMessage(publishingService, null, MessageIntentEnum.Subscribe, new ControlMessage());
        }

        public void Subscribe<T>()
        {
            Subscribe(typeof(T));
        }

        public void Subscribe<T>(string publishingService)
        {
            Subscribe(publishingService, typeof(T));
        }

        public void Unsubscribe(Type messageType)
        {
            Unsubscribe(null, messageType);
        }

        public void Unsubscribe(string publishingService, Type messageType)
        {
            if (string.IsNullOrWhiteSpace(publishingService))
            {
                publishingService = GetDestinationServiceForMessage(messageType);
            }
            Logger.Info("Unsubscribing from " + messageType.AssemblyQualifiedName + " at publisher " + publishingService);
            this.SetHeader(SubscriptionMessageType, messageType.AssemblyQualifiedName);
            SendMessage(publishingService, null, MessageIntentEnum.Unsubscribe, new ControlMessage());
        }

        public void Unsubscribe<T>()
        {
            Unsubscribe(typeof(T));
        }

        public void Unsubscribe<T>(string publishingService)
        {
            Unsubscribe(publishingService, typeof(T));
        }

        public void SendLocal(params object[] messages)
        {
            throw new NotImplementedException();
        }

        public void SendLocal<T>(Action<T> messageConstructor)
        {
            throw new NotImplementedException();
        }

        void ISendOnlyMessageBus.Send(params object[] messages)
        {
            var destination = GetDestinationServiceForMessages(messages);
            SendMessage(destination, null, MessageIntentEnum.Send, messages);
        }

        void ISendOnlyMessageBus.Send<T>(Action<T> messageConstructor)
        {
            ((IMessageBus)this).Send(CreateInstance(messageConstructor));
        }

        void ISendOnlyMessageBus.Send(string destinationService, params object[] messages)
        {
            SendMessage(destinationService, null, MessageIntentEnum.Send, messages);
        }

        void ISendOnlyMessageBus.Send<T>(string destinationService, Action<T> messageConstructor)
        {
            SendMessage(destinationService, null, MessageIntentEnum.Send, CreateInstance(messageConstructor));
        }

        void ISendOnlyMessageBus.Send(string destinationService, Guid correlationId, params object[] messages)
        {
            SendMessage(destinationService, correlationId, MessageIntentEnum.Send, messages);
        }

        void ISendOnlyMessageBus.Send<T>(string destinationService, Guid correlationId, Action<T> messageConstructor)
        {
            SendMessage(destinationService, correlationId, MessageIntentEnum.Send, CreateInstance(messageConstructor));
        }

        public void Reply(params object[] messages)
        {
            if (CurrentMessageContext == null)
                throw new InvalidOperationException("CurrentMessageContext is null. Cannot reply.");
            SendMessage(CurrentMessageContext.ReturnAddress, null, MessageIntentEnum.Send, messages);
        }

        public void Reply<T>(Action<T> messageConstructor)
        {
            Reply(CreateInstance<T>(messageConstructor));
        }

        private void SendMessage(string destination, Guid? correlationId, MessageIntentEnum messageIntent, params object[] messages)
        {
            if (messages == null || messages.Length == 0)
                throw new InvalidOperationException("Cannot send an empty set of messages.");

            if (string.IsNullOrEmpty(destination))
                throw new InvalidOperationException(string.Format("No destination specified for message {0}. Message cannot be sent.", messages[0].GetType().FullName));

            SendMessage(new List<string> { destination }, correlationId, messageIntent, messages);
        }

        private void SendMessage(IEnumerable<string> destinations, Guid? correlationId, MessageIntentEnum messageIntent, params object[] messages)
        {
            if (destinations == null || !destinations.Any() || destinations.Any(x => string.IsNullOrEmpty(x)))
            {
                throw new InvalidOperationException("No destination specified for message(s): " +
                                                            string.Join(";", messages.Select(m => m.GetType())));
            }

            var toSend = new TransportMessage { CorrelationId = correlationId.HasValue ? correlationId.ToString() : null, MessageIntent = messageIntent };
            MapTransportMessageFor(messages, toSend);

            transport.Send(toSend, destinations);
            OnMessageSent();
        }

        private TransportMessage MapTransportMessageFor(object[] messages, TransportMessage toSend)
        {
            toSend.Body = messages;
            toSend.WindowsIdentityName = Thread.CurrentPrincipal.Identity.Name;
            toSend.Headers = OutgoingHeaders.Select(x => new HeaderInfo() { Key = x.Key, Value = x.Value }).ToList();
            var timeToBeReceived = TimeSpan.MaxValue;
            toSend.TimeToBeReceived = timeToBeReceived;
            return toSend;
        }

        private void Transport_FailedMessageProcessing(object sender, FailedMessageProcessingEventArgs e)
        {
            OnMessageFailed();
        }

        private void Transport_TransportMessageReceived(object sender, TransportMessageReceivedEventArgs e)
        {
            this.OutgoingHeaders.Clear();
            _messageBeingHandled = e.Message;
            Logger.Debug("Received transport message with ID " + e.Message.Id + " from sender " + e.Message.ReturnAddress);
            OnMessageReceived();
            if (e.Message.Body.Any(x => x is ControlMessage))
            {
                if (HandleControlMessage())
                    return;
            }
            var sw = Stopwatch.StartNew();
            try
            {
                if (e.Message.MessageIntent == MessageIntentEnum.Send ||
                    e.Message.MessageIntent == MessageIntentEnum.Publish)
                {
                    using (var childServiceLocator = this.messageDispatcher.ServiceLocator.GetChildServiceLocator())
                    {
                        childServiceLocator.RegisterComponent<IMessageBus>(this);
                        this.messageDispatcher.DispatchMessages(childServiceLocator, e.Message.Body, CurrentMessageContext);
                    }
                }
                sw.Stop();
                OnMessageHandled(sw.ElapsedMilliseconds, sw.ElapsedTicks);
            }
            catch (Exception ex)
            {
                var orignalException = ex;
                if (orignalException is TargetInvocationException)
                    orignalException = ex.InnerException;
                throw new TransportMessageHandlingFailedException(orignalException);
            }
            finally
            {
                sw.Stop();
            }
            Logger.Debug("Finished handling message.");
        }

        private bool HandleControlMessage()
        {
            if (_messageBeingHandled.MessageIntent == MessageIntentEnum.Subscribe ||
                _messageBeingHandled.MessageIntent == MessageIntentEnum.Unsubscribe)
            {
                var messageTypeString = this.GetHeader(SubscriptionMessageType);
                if (subscriptionStorage == null)
                {
                    var warning = string.Format("Subscription message from {0} arrived at this endpoint, yet this endpoint is not configured to be a publisher.", _messageBeingHandled.ReturnAddress);
                    Logger.Warn(warning);
                    return true;
                }
                if (_messageBeingHandled.MessageIntent == MessageIntentEnum.Subscribe)
                {
                    Logger.Info("Subscribing " + _messageBeingHandled.ReturnAddress + " to message type " + messageTypeString);
                    subscriptionStorage.Subscribe(_messageBeingHandled.ReturnAddress, new[] { new MessageType(messageTypeString) });
                }
                if (_messageBeingHandled.MessageIntent == MessageIntentEnum.Unsubscribe)
                {
                    Logger.Info("Unsubscribing " + _messageBeingHandled.ReturnAddress + " from message type " + messageTypeString);
                    subscriptionStorage.Unsubscribe(_messageBeingHandled.ReturnAddress, new[] { new MessageType(messageTypeString) });
                }
                return true;
            }
            return false;
        }

        public void HandleCurrentMessageLater()
        {
            throw new NotImplementedException();
        }

        public void ForwardCurrentMessageTo(string destination)
        {
            throw new NotImplementedException();
        }

        public void DoNotContinueDispatchingCurrentMessageToHandlers()
        {
            throw new NotImplementedException();
        }

        [ThreadStatic]
        public Dictionary<string, string> outgoingHeaders = new Dictionary<string, string>();
        public IDictionary<string, string> OutgoingHeaders
        {
            get { return outgoingHeaders; }
        }

        [ThreadStatic]
        static TransportMessage _messageBeingHandled;
        public IMessageContext CurrentMessageContext
        {
            get { return _messageBeingHandled != null ? new MessageContext(_messageBeingHandled) : null; }
        }

        public T CreateInstance<T>()
        {
            return messageMapper.CreateInstance<T>();
        }

        public T CreateInstance<T>(Action<T> action)
        {
            return messageMapper.CreateInstance(action);
        }

        public object CreateInstance(Type messageType)
        {
            return messageMapper.CreateInstance(messageType);
        }

        private List<Type> GetFullTypes(IEnumerable<object> messages)
        {
            var types = new List<Type>();

            foreach (var m in messages)
            {
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

        public void MapMessageTypesToAddress(IDictionary<Type, string> mapping)
        {
            foreach (var item in mapping)
            {
                MapMessageTypeToAddress(item.Key, item.Value);
            }
        }

        public void MapMessageTypeToAddress(Type messageType, string address)
        {
            messageTypeToDestinationLocker.EnterWriteLock();
            messageTypeToDestinationLookup[messageType] = address;
            messageTypeToDestinationLocker.ExitWriteLock();
            Logger.Debug("Message " + messageType.FullName + " has been allocated to endpoint " + address + ".");
            return;
        }

        private string GetDestinationServiceForMessages(object[] messages)
        {
            if (!messages.Any()) return null;
            return GetDestinationServiceForMessage(messages.First().GetType());
        }

        private string GetDestinationServiceForMessage(Type messageType)
        {
            string destination;

            messageTypeToDestinationLocker.EnterReadLock();
            var destinationFound = messageTypeToDestinationLookup.TryGetValue(messageType, out destination);
            messageTypeToDestinationLocker.ExitReadLock();

            if (destinationFound)
                return destination;

            if (messageMapper != null && !messageType.IsInterface)
            {
                var t = messageMapper.GetMappedTypeFor(messageType);
                if (t != null && t != messageType)
                    return GetDestinationServiceForMessage(t);
            }

            return destination;
        }

        private bool hasStarted;
        private object startLock = new object();

        public void Start()
        {
            if (hasStarted) return;
            lock (startLock)
            {
                if (hasStarted) return;
                transport.Start();
                foreach (var additionalListener in additionalListeners)
                {
                    additionalListener.Start();
                }
                hasStarted = true;
            }
        }

        public event EventHandler MessageReceived;
        public event EventHandler MessageSent;
        public event EventHandler MessageFailed;
        public event EventHandler<MessageHandledEventArgs> MessageHandled;

        protected virtual void OnMessageReceived()
        {
            if (MessageReceived != null)
            {
                MessageReceived(this, EventArgs.Empty);
            }
        }

        protected virtual void OnMessageSent()
        {
            if (MessageSent != null)
            {
                MessageSent(this, EventArgs.Empty);
            }
        }

        protected virtual void OnMessageFailed()
        {
            if (MessageFailed != null)
            {
                MessageFailed(this, EventArgs.Empty);
            }
        }

        protected virtual void OnMessageHandled(long elapsedMilliseconds, long elapsedTicks)
        {
            if (MessageHandled != null)
            {
                MessageHandled(this, new MessageHandledEventArgs() { ElapsedMilliseconds = elapsedMilliseconds, ElapsedTicks = elapsedTicks });
            }
        }
    }
}
