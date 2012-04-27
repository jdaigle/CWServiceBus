using System.Collections.Generic;

namespace CWServiceBus.Unicast.Sql
{
    public interface ISubscriptionCacheProvider
    {
        bool Get(IEnumerable<MessageType> messageTypes, out List<string> subscribers);
        void Set(IEnumerable<MessageType> messageTypes, List<string> subscribers);
        void Clear(IEnumerable<MessageType> messageTypes);
        void ClearAll();
    }
}
