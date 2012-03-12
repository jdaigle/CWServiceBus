using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using log4net;

namespace CWServiceBus.Dispatch {
    public class MessageHandlerCollection {

        public static ILog Logger = LogManager.GetLogger(typeof(MessageHandlerCollection).Namespace);
        private bool isInit;
        private ISet<Assembly> assembliesToScan = new HashSet<Assembly>();
        private ISet<Type> additionalMessageHandlerTypes = new HashSet<Type>();
        private MessageTypeConventions messageTypeConventions;

        /// <summary>
        /// Dictionary keyed by MessageHandlerType, contains list of handled messages per message handler
        /// </summary>
        private readonly IDictionary<Type, List<Type>> handlerList = new Dictionary<Type, List<Type>>();
        private List<Type> orderedMessageHandlerList = new List<Type>();

        //private IDictionary<Type, IList<DispatchInfo>> messageHandlers = new Dictionary<Type, IList<DispatchInfo>>();

        /// <summary>
        /// Scans all assemblies and returns a distinct list of all classes which implement IMessageHandler<>
        /// </summary>
        public static IList<Type> FindAllMessageHandlerTypes(IEnumerable<Assembly> assemblies) {
            var types = new List<Type>();
            foreach (var assembly in assemblies) {
                types.AddRange(assembly.GetTypes().Where(t => t.IsMessageHandlerClassType()));
            }
            return types.Distinct().ToList();
        }

        /// <summary>
        /// Scans all types and returns a distinct list of all classes which implement IMessageHandler<>
        /// </summary>
        public static IList<Type> FindAllMessageHandlerTypes(IEnumerable<Type> types) {
            return types.Where(t => t.IsMessageHandlerClassType()).Distinct().ToList();
        }

        public MessageHandlerCollection() {
            this.messageTypeConventions = MessageTypeConventions.Default;
        }

        public MessageHandlerCollection(MessageTypeConventions messageTypeConventions) {
            this.messageTypeConventions = messageTypeConventions;
        }

        private void AssertNotInit() {
            if (isInit) throw new InvalidOperationException("Collection already initialized");
        }

        private void AssertInit() {
            if (!isInit) throw new InvalidOperationException("Collection has not been initialized");
        }

        public void Init() {
            if (isInit) return;

            var messageHandlerTypes =
                FindAllMessageHandlerTypes(assembliesToScan)
                .Concat(FindAllMessageHandlerTypes(additionalMessageHandlerTypes))
                .Distinct().ToList();

            foreach (Type messageHandlerType in messageHandlerTypes) {
                foreach (var messageType in messageHandlerType.GetMessageTypesIfIsMessageHandler()) {
                    if (!handlerList.ContainsKey(messageHandlerType))
                        handlerList.Add(messageHandlerType, new List<Type>());

                    if (!(handlerList[messageHandlerType].Contains(messageType))) {
                        handlerList[messageHandlerType].Add(messageType);
                        Logger.DebugFormat("Associated '{0}' message with '{1}' handler", messageType, messageHandlerType);
                    }
                }
            }
            orderedMessageHandlerList = handlerList.Keys.ToList();

            this.isInit = true;
        }

        public void ExecuteTheseHandlersFirst(params Type[] handlerTypes) {
            AssertInit();
            var firstOrderedHandlers = new HashSet<Type>();
            foreach (var handler in handlerTypes) {
                var firstOrderedHandler = orderedMessageHandlerList.FirstOrDefault(x => handler.IsAssignableFrom(x));
                if (firstOrderedHandler != null && !firstOrderedHandlers.Contains(firstOrderedHandler)) {
                    firstOrderedHandlers.Add(firstOrderedHandler);
                }
            }
            var allOtherHandlers = orderedMessageHandlerList.Except(firstOrderedHandlers).ToList();
            orderedMessageHandlerList = firstOrderedHandlers.Concat(allOtherHandlers).ToList();
        }

        public void AddAssembliesToScan(IEnumerable<Assembly> messageHandlerAssemblies) {
            AssertNotInit();
            foreach (var assembly in messageHandlerAssemblies) {
                assembliesToScan.Add(assembly);
            }
        }

        public void AddAssembliesToScan(params Assembly[] messageHandlerAssemblies) {
            AssertNotInit();
            AddAssembliesToScan((IEnumerable<Assembly>)messageHandlerAssemblies);
        }

        public void AddAssemblyToScan(Assembly messageHandlerAssembly) {
            AssertNotInit();
            AddAssembliesToScan(new[] { messageHandlerAssembly });
        }

        public void AddAdditonalMessageHandlerTypes(IEnumerable<Type> messageHandlerTypes) {
            AssertNotInit();
            foreach (var type in messageHandlerTypes) {
                additionalMessageHandlerTypes.Add(type);
            }
        }

        public void AddAdditonalMessageHandlerTypes(params Type[] messageHandlerTypes) {
            AssertNotInit();
            AddAdditonalMessageHandlerTypes((IEnumerable<Type>)messageHandlerTypes);
        }

        public void AddAdditonalMessageHandlerType(Type messageHandlerType) {
            AssertNotInit();
            AddAdditonalMessageHandlerTypes(new[] { messageHandlerType });
        }

        public IEnumerable<Type> AllMessageTypes() {
            AssertInit();
            foreach (var handlerType in handlerList.Keys)
                foreach (var typeHandled in handlerList[handlerType])
                    if (messageTypeConventions.IsMessageType(typeHandled))
                        yield return typeHandled;
        }

        public IEnumerable<DispatchInfo> GetOrderedDispatchInfoFor(Type messageType) {
            AssertInit();
            foreach (var messageHandlerType in GetHandlerTypes(messageType)) {
                // TODO: somehow cache the DispatchInfo?
                yield return new DispatchInfo(messageType, messageHandlerType, GetHandleMethod(messageHandlerType, messageType));
            }
        }

        private IEnumerable<Type> GetHandlerTypes(Type messageType) {
            foreach (var handlerType in orderedMessageHandlerList)
                foreach (var msgTypeHandled in handlerList[handlerType])
                    if (msgTypeHandled.IsAssignableFrom(messageType)) {
                        yield return handlerType;
                        break;
                    }
        }

        static MethodInfo GetHandleMethod(Type targetType, Type messageType) {
            var method = targetType.GetMethod("Handle", new[] { messageType });
            if (method != null) return method;

            var handlerType = typeof(IMessageHandler<>).MakeGenericType(messageType);
            return targetType.GetInterfaceMap(handlerType)
                .TargetMethods
                .FirstOrDefault();
        }
    }
}
