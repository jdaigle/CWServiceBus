using System;
using System.Collections.Generic;

namespace CWServiceBus.CommandBus.Unicast {
    public class MessageContext : IMessageContext {
        IDictionary<string, string> IMessageContext.Headers {
            get { return new Dictionary<string, string>(); }
        }

        Guid IMessageContext.MessageId {
            get { return Guid.Empty; }
        }

        string IMessageContext.ReturnAddress {
            get { return null; }
        }

        DateTime timeSent = DateTime.UtcNow;
        DateTime IMessageContext.TimeSent {
            get { return timeSent; }
        }
    }
}
