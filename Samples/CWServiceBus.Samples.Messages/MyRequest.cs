using System;

namespace CWServiceBus.Samples.Messages {
    public interface MyRequest {
        Guid MyRequestId { get; set; }
        string MyRequestData { get; set; }
    }
}
