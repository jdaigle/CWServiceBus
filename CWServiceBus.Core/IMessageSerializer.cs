using System.IO;

namespace CWServiceBus
{
    public interface IMessageSerializer
    {
        void Serialize(object[] messages, Stream stream);
        object[] Deserialize(Stream stream);
    }
}
