﻿using CWServiceBus.Transport;

namespace CWServiceBus.Config {
    public interface ITransportBuilder {
        ITransport Build();
        string EndpointName { get; }
    }
}
