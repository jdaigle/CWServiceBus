using System;

namespace CWServiceBus.Transport {
    public class TransportMessageHandlingFailedException : Exception {
        public Exception OriginalException { get; set; }

        public TransportMessageHandlingFailedException(Exception originalException) {
            OriginalException = originalException;
        }
    }
}