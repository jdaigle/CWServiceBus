using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CWServiceBus.ServiceBroker;
using CWServiceBus.Unicast;
using CWServiceBus.Reflection;
using CWServiceBus.Dispatch;
using CWServiceBus.Samples.Messages;
using CWServiceBus.Serializers.XML;

namespace CWServiceBus.Samples.RequestServerService {
    public class Program {
        public static void Main() {
            //var serviceBus = ServiceBusBuilder.Initialize(builder => {
            //    builder.MessageTypeConventions.AddConvention(t => t.Namespace == "CWServiceBus.Samples.Messages");
            //});
            
            var messageTypeConventions = new MessageTypeConventions();
            IEnumerable<Type> additionalMessageTypes = new Type[0];

            var messageHandlers = new MessageHandlerCollection(messageTypeConventions);
            messageHandlers.AddAssemblyToScan(typeof(MyRequest).Assembly);
            messageHandlers.Init();

            var messageMapper = new MessageMapper();
            messageMapper.SetMessageTypeConventions(messageTypeConventions);
            messageMapper.Initialize(messageHandlers.AllMessageTypes().Concat(additionalMessageTypes).Distinct());

            var serializer = new XmlMessageSerializer(messageMapper);
            serializer.Initialize(messageHandlers.AllMessageTypes().Concat(additionalMessageTypes).Distinct());

            var serviceBus = new UnicastServiceBus();

        }
    }
}
