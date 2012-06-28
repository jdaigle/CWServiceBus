using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using CWServiceBus.Transport;

namespace CWServiceBus.Serializers
{
    public class XmlTransportMessageSerializer : ITransportMessageSerializer
    {
        private static XmlSerializer xmlSerializerForSerialization;
        private static XmlSerializer xmlSerializerForDeserialization = new XmlSerializer(typeof(TransportMessage));
        private static object GetXmlSerializerLock = new object();

        static XmlSerializer GetXmlSerializer()
        {
            lock (GetXmlSerializerLock)
            {
                if (xmlSerializerForSerialization == null)
                {
                    var overrides = new XmlAttributeOverrides();
                    var attrs = new XmlAttributes { XmlIgnore = true };

                    overrides.Add(typeof(TransportMessage), "Messages", attrs);
                    xmlSerializerForSerialization = new XmlSerializer(typeof(TransportMessage), overrides);
                }
                return xmlSerializerForSerialization;
            }
        }

        private readonly IMessageSerializer messageSerializer;

        public XmlTransportMessageSerializer(IMessageSerializer messageSerializer)
        {
            this.messageSerializer = messageSerializer;
        }

        public void Serialize(TransportMessage transportMessage, Stream outputStream)
        {
            var xs = GetXmlSerializer();
            var doc = new XmlDocument();

            using (var tempstream = new MemoryStream())
            {
                xs.Serialize(tempstream, transportMessage);
                tempstream.Position = 0;

                doc.Load(tempstream);
            }

            if (transportMessage.Body != null && transportMessage.BodyStream == null)
            {
                transportMessage.BodyStream = new MemoryStream();
                this.messageSerializer.Serialize(transportMessage.Body, transportMessage.BodyStream);
            }

            // Reset the stream, so that we can read it back out as data
            transportMessage.BodyStream.Position = 0;

            var data = new StreamReader(transportMessage.BodyStream).ReadToEnd();
            var bodyElement = doc.CreateElement("Body");
            bodyElement.AppendChild(doc.CreateCDataSection(data));
            doc.DocumentElement.AppendChild(bodyElement);

            doc.Save(outputStream);
            outputStream.Position = 0;
        }

        public TransportMessage Deserialize(Stream inputStream)
        {
            var transportMessage = (TransportMessage)xmlSerializerForDeserialization.Deserialize(inputStream);
            inputStream.Position = 0;

            var bodyDoc = new XmlDocument();
            bodyDoc.Load(inputStream);

            var payLoad = bodyDoc.DocumentElement.SelectSingleNode("Body").FirstChild as XmlCDataSection;
            transportMessage.Body = ExtractMessages(payLoad);

            return transportMessage;
        }

        private object[] ExtractMessages(XmlCDataSection data)
        {
            var messages = new XmlDocument();
            messages.LoadXml(data.Data);
            using (var stream = new MemoryStream())
            {
                using (var xmlWriter = new XmlTextWriter(stream, Encoding.UTF8))
                {
                    xmlWriter.Formatting = Formatting.None;
                    messages.Save(xmlWriter);
                    stream.Position = 0;
                    return this.messageSerializer.Deserialize(stream);
                }
            }
        }
    }
}
