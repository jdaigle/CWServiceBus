using System;

namespace CWServiceBus.Transport {
    public class TransportMessageFaultEventArgs : EventArgs {
        public TransportMessageFaultEventArgs(TransportMessage m, Exception e, string reason) {
            Message = m;
            Exception = e;
            Reason = reason;
        }

        public TransportMessage Message { get; private set; }
        public Exception Exception { get; private set; }
        public string Reason { get; private set; }
    }
}
