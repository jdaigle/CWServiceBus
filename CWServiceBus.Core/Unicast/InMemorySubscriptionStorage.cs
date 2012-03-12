using System.Collections.Concurrent;
using System.Collections.Generic;

namespace CWServiceBus.Unicast {
    public class InMemorySubscriptionStorage : ISubscriptionStorage {
        public void Subscribe(string destinationService, IEnumerable<MessageType> messageTypes) {
            foreach (var m in messageTypes) {
                if (!storage.ContainsKey(m))
                    storage[m] = new List<string>();

                if (!storage[m].Contains(destinationService))
                    storage[m].Add(destinationService);
            }
        }

        public void Unsubscribe(string destinationService, IEnumerable<MessageType> messageTypes) {
            foreach (var m in messageTypes) {
                if (storage.ContainsKey(m))
                    storage[m].Remove(destinationService);
            }
        }

        public IEnumerable<string> GetSubscriberServicesForMessage(IEnumerable<MessageType> messageTypes) {
            var result = new List<string>();
            foreach (var m in messageTypes) {
                if (storage.ContainsKey(m))
                    result.AddRange(storage[m]);
            }

            return result;
        }

        public void Init() {
        }

        readonly ConcurrentDictionary<MessageType, List<string>> storage = new ConcurrentDictionary<MessageType, List<string>>();
    }
}
