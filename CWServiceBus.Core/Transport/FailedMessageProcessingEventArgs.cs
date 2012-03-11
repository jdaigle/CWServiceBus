using System;

namespace CWServiceBus.Transport {
    public class FailedMessageProcessingEventArgs : EventArgs {
        public Exception Reason { get; private set; }

        public FailedMessageProcessingEventArgs(Exception ex) {
            Reason = ex;
        }
    }
}