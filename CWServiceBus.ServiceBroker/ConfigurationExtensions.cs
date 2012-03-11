using System;

namespace CWServiceBus.ServiceBroker {
    public static class ConfigurationExtensions {
        public static ServiceBusBuilder UseServiceBrokerTransport(this ServiceBusBuilder builder, Action<Config.ServiceBrokerTransportBuilder> configure) {
            builder.TransportBuilder = new Config.ServiceBrokerTransportBuilder(builder);
            if (configure != null)
                configure(builder.TransportBuilder as Config.ServiceBrokerTransportBuilder);
            return builder;
        }
    }
}
