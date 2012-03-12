﻿namespace CWServiceBus {
    public static class MessageBusExtensionsMethods {
        public static void CopyHeaderFromRequest(this IMessageBus messageBus) {
            foreach (var item in messageBus.CurrentMessageContext.Headers) {
                messageBus.OutgoingHeaders[item.Key] = item.Value;
            }
        }

        public static string GetHeader(this IMessageBus messageBus, string key) {
            return messageBus.CurrentMessageContext.Headers.ContainsKey(key) ? messageBus.CurrentMessageContext.Headers[key] : string.Empty;
        }

        public static string SetHeader(this IMessageBus messageBus, string key, string value) {
            return messageBus.OutgoingHeaders[key] = value;
        }
    }
}
