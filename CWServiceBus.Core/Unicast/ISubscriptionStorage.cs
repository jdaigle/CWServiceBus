using System.Collections.Generic;

namespace CWServiceBus.Unicast {
    public interface ISubscriptionStorage {
        void Subscribe(string destinationService, IEnumerable<MessageType> messageTypes);
        void Unsubscribe(string destinationService, IEnumerable<MessageType> messageTypes);
        IEnumerable<string> GetSubscriberServicesForMessage(IEnumerable<MessageType> messageTypes);
    }
}
