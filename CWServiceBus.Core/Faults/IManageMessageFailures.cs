using System;
using CWServiceBus.Transport;

namespace CWServiceBus.Faults {
    public interface IManageMessageFailures {
        void SerializationFailedForMessage(TransportMessage message, Exception e);
        void ProcessingAlwaysFailsForMessage(TransportMessage message, Exception e);
    }
}
