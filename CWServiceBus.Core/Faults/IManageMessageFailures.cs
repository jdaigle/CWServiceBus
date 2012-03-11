using System;
using CWServiceBus.Transport;

namespace CWServiceBus.Faults {
    public interface IManageMessageFailures {
        void SerializationFailedForMessage(object underlyingTransportObject, TransportMessage message, Exception e);
        void ProcessingAlwaysFailsForMessage(object underlyingTransportObject, TransportMessage message, Exception e);
    }
}
