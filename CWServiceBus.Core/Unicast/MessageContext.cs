using System;
using System.Linq;
using System.Collections.Generic;
using CWServiceBus.Transport;

namespace CWServiceBus.Unicast
{
    public class MessageContext : IMessageContext
    {
        private TransportMessage transportMessage;

        public MessageContext(TransportMessage transportMessage)
        {
            this.transportMessage = transportMessage;
        }

        IDictionary<string, string> IMessageContext.Headers
        {
            get { return transportMessage.Headers.ToDictionary(x => x.Key, x => x.Value); }
        }

        Guid IMessageContext.MessageId
        {
            get { return new Guid(transportMessage.IdForCorrelation); }
        }

        string IMessageContext.ReturnAddress
        {
            get { return transportMessage.ReturnAddress.ToString(); }
        }

        DateTime IMessageContext.TimeSent
        {
            get { return transportMessage.TimeSent;  }
        }
    }
}
