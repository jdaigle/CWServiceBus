namespace CWServiceBus {
    public static class ServiceBusExtensionsMethods {
        public static void CopyHeaderFromRequest(this IServiceBus serviceBus) {
            foreach (var item in serviceBus.CurrentMessageContext.Headers) {
                serviceBus.OutgoingHeaders[item.Key] = item.Value;
            }
        }

        public static string GetHeader(this IServiceBus serviceBus, string key) {
            return serviceBus.CurrentMessageContext.Headers.ContainsKey(key) ? serviceBus.CurrentMessageContext.Headers[key] : string.Empty;
        }

        public static string SetHeader(this IServiceBus serviceBus, string key, string value) {
            return serviceBus.OutgoingHeaders[key] = value;
        }
    }
}
