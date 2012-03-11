using System;

namespace CWServiceBus.Samples.Messages {
    public interface MyResponse {
        Guid MyRequestId { get; set; }
        string MyResponseData { get; set; }
        DateTime MyResponseDataTime { get; set; }
    }
}
