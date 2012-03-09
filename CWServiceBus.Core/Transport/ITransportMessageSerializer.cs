using System.IO;

namespace CWServiceBus.Transport
{
    public interface ITransportMessageSerializer
    {
        void Serialize(TransportMessage transportMessage, Stream outputStream);
        TransportMessage Deserialize(Stream inputStream);
    }
}
