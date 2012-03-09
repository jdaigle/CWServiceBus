using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using CWServiceBus.Reflection;
using CWServiceBus.Serializers.XML;
using CWServiceBus.Serializers.XML.NS1;
using CWServiceBus.Serializers.XML.NS2;
using System.IO;
using CWServiceBus.Transport;
using System.Diagnostics;

namespace CWServiceBus.Serializers {
    [TestFixture]
    public class TransportMessageSerializerTests {
        [Test]
        public void Serialize_Deserialize_TransportMessage() {
            var types = new List<Type> { typeof(C1), typeof(C2) };
            var mapper = new MessageMapper();
            mapper.Initialize(types);
            var serializer = new XmlMessageSerializer(mapper);
            serializer.Initialize(types);

            var transportMessage = new TransportMessage();
            transportMessage.Id = Guid.NewGuid().ToString();
            transportMessage.IdForCorrelation = Guid.NewGuid().ToString();
            transportMessage.ReturnAddress = Guid.NewGuid().ToString();
            transportMessage.WindowsIdentityName = string.Empty;
            transportMessage.TimeSent = DateTime.Now;
            transportMessage.Headers = new List<HeaderInfo>();
            transportMessage.Body = new object[] { new C1() { Data = "o'tool" }, new C2() { Data = "Timmy" } };

            var newTransportMessage = Execute(transportMessage, serializer);

            var messages = newTransportMessage.Body;
            Assert.AreEqual(2, messages.Count());
            Assert.AreEqual(1, messages.Count(x => x is C1));
            Assert.AreEqual(1, messages.Count(x => x is C2));
            Assert.AreEqual("o'tool", ((C1)messages.First(x => x is C1)).Data);
            Assert.AreEqual("Timmy", ((C2)messages.First(x => x is C2)).Data);
            Assert.AreEqual(transportMessage.Id, newTransportMessage.Id);
            Assert.AreEqual(transportMessage.IdForCorrelation, newTransportMessage.IdForCorrelation);
            Assert.AreEqual(transportMessage.ReturnAddress, newTransportMessage.ReturnAddress);
            Assert.AreEqual(transportMessage.TimeSent, newTransportMessage.TimeSent);
            Assert.AreEqual(transportMessage.Headers, newTransportMessage.Headers);
            Assert.AreNotSame(transportMessage, newTransportMessage);
        }

        public static TransportMessage Execute(TransportMessage transportMessage, IMessageSerializer messageSerializer) {
            var serializer = new XmlTransportMessageSerializer(messageSerializer);

            using (var stream = new MemoryStream()) {
                serializer.Serialize(transportMessage, stream);
                Debug.WriteLine(Encoding.ASCII.GetString(stream.ToArray()));
                stream.Position = 0;
                return serializer.Deserialize(stream);
            }
        }
    }
}
