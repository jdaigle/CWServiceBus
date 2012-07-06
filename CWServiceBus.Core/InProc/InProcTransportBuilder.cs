using CWServiceBus.Config;
using CWServiceBus.Transport;

namespace CWServiceBus.InProc
{
    public sealed class InProcTransportBuilder : ITransportBuilder
    {
        ITransport ITransportBuilder.Build()
        {
            return new InProcTransport();
        }

        string ITransportBuilder.EndpointName
        {
            get { return "In Process"; }
        }
    }
}
