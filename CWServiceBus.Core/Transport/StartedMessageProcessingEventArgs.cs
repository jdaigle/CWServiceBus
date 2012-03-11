using System;

namespace CWServiceBus.Transport {
    public class StartedMessageProcessingEventArgs : EventArgs {
        public StartedMessageProcessingEventArgs(TransportMessage m) {
            message = m;
        }

        readonly TransportMessage message;

        public TransportMessage Message {
            get { return message; }
        }
    }
}