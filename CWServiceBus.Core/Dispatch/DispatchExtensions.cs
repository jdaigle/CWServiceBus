﻿using System;
using System.Linq;
using System.Reflection;

namespace CWServiceBus.Dispatch {
    public static class DispatchExtensions {
        private readonly static Type messageHandlerOpenGenericType = typeof(IMessageHandler<>);
        private readonly static string messageHandlerHandleMethodName = "Handle";

        public static bool IsMessageHandlerClassType(this Type type) {
            return type.IsClass && !type.IsAbstract &&
                   type.GetInterfaces().Any(i => i.IsMessageHandlerInterfaceType());
        }

        public static bool IsMessageHandlerClassTypeForMessageType(this Type type, Type messageType) {
            return type.IsClass && !type.IsAbstract &&
                   type.GetInterfaces().Any(i => i.IsMessageHandlerInterfaceTypeClosedBy(messageType));
        }

        public static bool IsMessageHandlerInterfaceType(this Type type) {
            return type.IsGenericType && type.GetGenericTypeDefinition() == messageHandlerOpenGenericType;
        }

        public static bool IsMessageHandlerInterfaceTypeClosedBy(this Type type, Type messageType) {
            return type.IsMessageHandlerInterfaceType() && messageType.IsAssignableFrom(type.GetGenericArguments().FirstOrDefault());
        }

        public static bool IsHandleMethodForMessageType(this MethodInfo method, Type messageType) {
            return method.Name == messageHandlerHandleMethodName &&
                   method.IsPublic &&
                   method.GetParameters().Count() == 1 &&
                   messageType.IsAssignableFrom(method.GetParameters().Single().ParameterType);
        }
    }
}
