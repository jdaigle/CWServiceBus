using System;

namespace CWServiceBus.Transport {
    public class TransportMessageReceivedEventArgs : EventArgs {
        public TransportMessageReceivedEventArgs(TransportMessage m) {
            message = m;
        }

        private readonly TransportMessage message;

        public TransportMessage Message {
            get { return message; }
        }
    }
}
