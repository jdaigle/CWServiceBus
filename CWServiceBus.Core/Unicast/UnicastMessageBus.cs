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
        private ITransport transport;
        private IList<ITransport> additionalListeners = new List<ITransport>();
        private IMessageDispatcher messageDispatcher;

        public UnicastMessageBus() { }

        public UnicastMessageBus(IMessageMapper messageMapper, ITransport transport, IMessageDispatcher messageDispatcher)
        {
            this.messageMapper = messageMapper;
            this.Transport = transport;
            this.messageDispatcher = messageDispatcher;
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

        public void SendLocal(params object[] messages)
        {
            SendMessage(this.transport.ReturnAddress, null, MessageIntentEnum.Send, messages);
        }

        public void SendLocal<T>(Action<T> messageConstructor)
        {
            ((IMessageBus)this).SendLocal(CreateInstance(messageConstructor));
        }

        void IMessageBus.Send(params object[] messages)
        {
            var destination = GetDestinationServiceForMessages(messages);
            SendMessage(destination, null, MessageIntentEnum.Send, messages);
        }

        void IMessageBus.Send<T>(Action<T> messageConstructor)
        {
            ((IMessageBus)this).Send(CreateInstance(messageConstructor));
        }

        void IMessageBus.Send(string destinationService, params object[] messages)
        {
            SendMessage(destinationService, null, MessageIntentEnum.Send, messages);
        }

        void IMessageBus.Send<T>(string destinationService, Action<T> messageConstructor)
        {
            SendMessage(destinationService, null, MessageIntentEnum.Send, CreateInstance(messageConstructor));
        }

        void IMessageBus.Send(IEnumerable<string> destinations, params object[] messages)
        {
            SendMessage(destinations, null, MessageIntentEnum.Send, messages);
        }

        void IMessageBus.Send<T>(IEnumerable<string> destinations, Action<T> messageConstructor)
        {
            SendMessage(destinations, null, MessageIntentEnum.Send, CreateInstance(messageConstructor));
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
            OnMessageSent(transport, destinations, messages);
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
            OnMessageFailed((ITransport)sender);
        }

        private void Transport_TransportMessageReceived(object sender, TransportMessageReceivedEventArgs e)
        {
            this.OutgoingHeaders.Clear();
            _messageBeingHandled = e.Message;
            Logger.Debug("Received transport message with ID " + e.Message.Id + " from sender " + e.Message.ReturnAddress);
            OnMessageReceived((ITransport)sender);
            var sw = Stopwatch.StartNew();
            try
            {
                if (e.Message.MessageIntent == MessageIntentEnum.Send)
                {
                    using (var childServiceLocator = this.messageDispatcher.ServiceLocator.GetChildServiceLocator())
                    {
                        childServiceLocator.RegisterComponent<IMessageBus>(this);
                        this.messageDispatcher.DispatchMessages(childServiceLocator, e.Message.Body, CurrentMessageContext);
                    }
                }
                sw.Stop();
                OnMessageHandled((ITransport)sender, e.Message, sw.Elapsed.TotalMilliseconds, sw.ElapsedTicks);
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
        private static Dictionary<string, string> outgoingHeaders;
        public IDictionary<string, string> OutgoingHeaders
        {
            get
            {
                if (outgoingHeaders == null)
                {
                    outgoingHeaders = new Dictionary<string, string>();
                }
                return outgoingHeaders;
            }
        }

        [ThreadStatic]
        private static TransportMessage _messageBeingHandled;
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
                {
                    continue;
                }
                types.Add(s);
                foreach (var t in s.GetInterfaces())
                {
                    if (messageMapper.IsMessageType(t))
                    {
                        if (!types.Contains(t))
                        {
                            types.Add(t);
                        }
                    }
                }
                var baseType = s.BaseType;
                while (baseType != null && baseType != typeof(object))
                {
                    if (messageMapper.IsMessageType(baseType))
                    {
                        if (!types.Contains(baseType))
                        {
                            types.Add(baseType);
                        }
                    }
                    baseType = baseType.BaseType;
                }
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
            {
                return destination;
            }

            if (!messageType.IsInterface)
            {
                var interfaces = messageType.GetInterfaces();
                foreach (var _interface in interfaces)
                {
                    messageTypeToDestinationLocker.EnterReadLock();
                    destinationFound = messageTypeToDestinationLookup.TryGetValue(_interface, out destination);
                    messageTypeToDestinationLocker.ExitReadLock();
                    if (destinationFound)
                    {
                        return destination;
                    }
                }
            }

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

        public event Action<ITransport> MessageReceived;
        public event Action<ITransport, IEnumerable<string>, IEnumerable<object>> MessageSent;
        public event Action<ITransport> MessageFailed;
        public event Action<ITransport, TransportMessage, double> MessageHandled;

        protected virtual void OnMessageReceived(ITransport transport)
        {
            if (MessageReceived != null)
            {
                MessageReceived(transport);
            }
        }

        protected virtual void OnMessageSent(ITransport transport, IEnumerable<string> destinations, IEnumerable<object> messages)
        {
            if (MessageSent != null)
            {
                MessageSent(transport, destinations, messages);
            }
        }

        protected virtual void OnMessageFailed(ITransport transport)
        {
            if (MessageFailed != null)
            {
                MessageFailed(transport);
            }
        }

        protected virtual void OnMessageHandled(ITransport transport, TransportMessage transportMessage, double elapsedMilliseconds, long elapsedTicks)
        {
            if (MessageHandled != null)
            {
                MessageHandled(transport, transportMessage, elapsedMilliseconds);
            }
        }
    }
}
