using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CWServiceBus.Transport;
using System.Data.SqlClient;
using System.Data;

namespace CWServiceBus.ServiceBroker.Transport {
    public class ServiceBrokerMessageReceiver : IReceiveMessages {

        public const string NServiceBusTransportMessageContract = "NServiceBusTransportMessageContract";
        public const string NServiceBusTransportMessage = "NServiceBusTransportMessage";

        private static readonly ILog Logger = LogManager.GetLogger(typeof(ServiceBrokerMessageReceiver).Namespace);
        private static readonly int waitTimeout = 21600 * 1000; // wait 6 hours
        public string ListenerQueue { get; set; }
        public ITransportMessageSerializer TransportMessageSerializer { get; set; }

        public void Init() {
            throw new NotImplementedException();
        }

        public TransportMessage Receive(ITransactionToken transactionToken, Action onAfterWaitingCallback) {
            var message = ServiceBrokerWrapper.WaitAndReceive(transactionToken as IDbTransaction, ListenerQueue, waitTimeout); // Wait 6 hours
            if (onAfterWaitingCallback != null) onAfterWaitingCallback();
            if (message == null)
                return null;
            // Only handle transport messages
            if (message.MessageTypeName == NServiceBusTransportMessage) {
                TransportMessage transportMessage = null;
                try {
                    transportMessage = TransportMessageSerializer.Deserialize(message.BodyStream);
                } catch (Exception e) {
                    Logger.Error("Could not extract message data.", e);
                    OnSerializationFailed(conversationHandle, message, e);
                    return null; // deserialization failed - no reason to try again, so don't throw
                }
            }
            return null;
        }
    }
}
