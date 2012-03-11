using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CWServiceBus.Dispatch {
    public class MessageHandlerCollection {

        private bool isInit;
        private ISet<Assembly> assembliesToScan = new HashSet<Assembly>();
        private ISet<Type> additionalMessageTypes = new HashSet<Type>();
        private IDictionary<Type, IList<DispatchInfo>> messageHandlers = new Dictionary<Type, IList<DispatchInfo>>();

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
        /// Scans all message handler types and returns a distinct list of all message types handled
        /// </summary>
        /// <remarks>
        /// For all class implementing the generic IMessageHandler<>
        /// Select each closed IMessageHandler<> interface, get the type used to the close the generic interface
        /// </remarks>
        public static IList<Type> FindAllMessageTypesForDispatch(IEnumerable<Type> messageHandlerTypes) {
            var types = new List<Type>();
            types.AddRange(messageHandlerTypes
                                   .SelectMany(x => x.GetInterfaces().Where(i => i.IsMessageHandlerInterfaceType())
                                                                     .Select(i => i.GetGenericArguments().FirstOrDefault())));
            return types.Distinct().ToList();
        }

        private MessageTypeConventions messageTypeConventions;

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
            var messageHandlerTypes = FindAllMessageHandlerTypes(assembliesToScan);
            IEnumerable<Type> messageTypes = FindAllMessageTypesForDispatch(messageHandlerTypes);
            messageTypes = messageTypes.Concat(additionalMessageTypes).Distinct();
            foreach (var messageType in messageTypes) {
                RegisterDispatchHandler(messageType, messageHandlerTypes);
            }
            this.isInit = true;
        }

        private void RegisterDispatchHandler(Type messageType, IEnumerable<Type> messageHandlerTypes) {
            if (!messageHandlers.ContainsKey(messageType))
                messageHandlers.Add(messageType, new List<DispatchInfo>());
            foreach (var instanceType in messageHandlerTypes.Where(t => t.IsMessageHandlerClassTypeForMessageType(messageType))) {
                foreach (var methodInfo in instanceType.GetMethods().Where(x => x.IsHandleMethodForMessageType(messageType))) {
                    if (messageHandlers[messageType].Any(x => x.MessageType == messageType &&
                                                              x.InstanceType == instanceType &&
                                                              x.MethodInfo == methodInfo)) {
                        continue;
                    }
                    messageHandlers[messageType].Add(new DispatchInfo(messageType, instanceType, methodInfo));
                }
            }

            if (messageType.IsClass && messageType.BaseType != typeof(object)) {
                if (messageTypeConventions.IsMessageType(messageType.BaseType))
                    RegisterDispatchHandler(messageType.BaseType, messageHandlerTypes);
            }
            foreach (var _interface in messageType.GetInterfaces()) {
                if (messageTypeConventions.IsMessageType(_interface))
                    RegisterDispatchHandler(_interface, messageHandlerTypes);
            }
        }

        public void ExecuteTheseHandlersFirst(params Type[] handlerTypes) {
            AssertInit();
            foreach (var messageType in messageHandlers.Keys.ToList()) {
                var handlers = messageHandlers[messageType];
                var firstOrderedHandlers = new HashSet<DispatchInfo>();
                foreach (var handler in handlerTypes) {
                    var firstOrderedHandler = handlers.FirstOrDefault(x => handler.IsAssignableFrom(x.InstanceType));
                    if (firstOrderedHandler != null && !firstOrderedHandlers.Contains(firstOrderedHandler)) {
                        firstOrderedHandlers.Add(firstOrderedHandler);
                    }
                }
                messageHandlers[messageType] = new List<DispatchInfo>(firstOrderedHandlers.Concat(handlers.Except(firstOrderedHandlers)));
            }
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

        public void AddAdditonalMessageTypes(IEnumerable<Type> messageTypes) {
            AssertNotInit();
            foreach (var type in messageTypes) {
                additionalMessageTypes.Add(type);
            }
        }

        public void AddAdditonalMessageTypes(params Type[] messageTypes) {
            AssertNotInit();
            AddAdditonalMessageTypes((IEnumerable<Type>)messageTypes);
        }

        public void AddAdditonalMessageType(Type messageType) {
            AssertNotInit();
            AddAdditonalMessageTypes(new[] { messageType });
        }

        public IEnumerable<Type> AllMessageTypes() {
            AssertInit();
            return messageHandlers.Keys;
        }

        public IEnumerable<DispatchInfo> GetOrderedHandlersFor(Type messageType) {
            AssertInit();
            if (messageHandlers.ContainsKey(messageType)) {
                return messageHandlers[messageType];
            } else {
                return new DispatchInfo[0];
            }
        }

        /* AFTER THIS LINE IS POSSIBLE NEW IMPLEMENTATION */

        /// <summary>
        /// Evaluates a type and loads it if it implements IMessageHander{T}.
        /// </summary>
        /// <param name="handler">The type to evaluate.</param>
        void IfTypeIsMessageHandlerThenLoad(Type handler) {
            if (handler.IsAbstract)
                return;


            foreach (var messageType in GetMessageTypesIfIsMessageHandler(handler)) {
                if (!handlerList.ContainsKey(handler))
                    handlerList.Add(handler, new List<Type>());

                if (!(handlerList[handler].Contains(messageType))) {
                    handlerList[handler].Add(messageType);
                    Log.DebugFormat("Associated '{0}' message with '{1}' handler", messageType, handler);
                }

                HandlerInvocationCache.CacheMethodForHandler(handler, messageType);
            }
        }


        /// <summary>
        /// If the type is a message handler, returns all the message types that it handles
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private static IEnumerable<Type> GetMessageTypesIfIsMessageHandler(Type type) {
            foreach (var t in type.GetInterfaces()) {
                if (t.IsGenericType) {
                    var args = t.GetGenericArguments();
                    if (args.Length != 1)
                        continue;

                    var handlerType = typeof(IMessageHandler<>).MakeGenericType(args[0]);
                    if (handlerType.IsAssignableFrom(t))
                        yield return args[0];
                }
            }
        }

        private readonly IDictionary<Type, List<Type>> handlerList = new Dictionary<Type, List<Type>>();
    }
}
