using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CWServiceBus.Transport;

namespace CWServiceBus {
    public class ServiceBusBuilder {
        public static IServiceBus Initialize(Action<ServiceBusBuilder> intialize) {
            var initializationContext = new ServiceBusBuilder();
            intialize(initializationContext);

            return null;
        }

        public ServiceBusBuilder() {
            this.MessageTypeConventions = new MessageTypeConventions();
        }

        public MessageTypeConventions MessageTypeConventions { get; private set; }

    }
}
