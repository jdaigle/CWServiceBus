using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CWServiceBus.Reflection;
using CWServiceBus.Transport;
using NUnit.Framework;

namespace CWServiceBus.Serializers.XML
{
    [TestFixture]
    public class EmptyStringSerializationTests
    {
        private IMessageSerializer serializer;

        [SetUp]
        public void Setup()
        {
            var types = new List<Type> { typeof(Sample_Message) };
            var mapper = new MessageMapper();
            mapper.Initialize(types);
            serializer = new XmlMessageSerializer(mapper);
            ((XmlMessageSerializer)serializer).Initialize(types);
        }

        [Test]
        public void No_Null_No_Empty()
        {
            var sample = new Sample_Message()
            {
                PatientMemberId = Guid.NewGuid().ToString(),
                PatientGroupNumber = Guid.NewGuid().ToString(),
            };

            var test = SerializeThenDeserialize(sample) as Sample_Message;

            Assert.AreEqual(sample.PatientMemberId, test.PatientMemberId);
            Assert.AreEqual(sample.PatientGroupNumber, test.PatientGroupNumber);
        }

        [Test]
        public void With_Null()
        {
            var sample = new Sample_Message()
            {
                PatientMemberId = Guid.NewGuid().ToString(),
                PatientGroupNumber = null,
            };

            var test = SerializeThenDeserialize(sample) as Sample_Message;

            Assert.AreEqual(sample.PatientMemberId, test.PatientMemberId);
            Assert.Null(test.PatientGroupNumber);
        }

        [Test]
        public void With_Empty()
        {
            var sample = new Sample_Message()
            {
                PatientMemberId = Guid.NewGuid().ToString(),
                PatientGroupNumber = string.Empty,
            };

            var test = SerializeThenDeserialize(sample) as Sample_Message;

            Assert.AreEqual(sample.PatientMemberId, test.PatientMemberId);
            Assert.AreEqual(string.Empty, test.PatientGroupNumber);
        }

        [Test]
        public void TransportMessage_No_Null_No_Empty()
        {
            var sample = new Sample_Message()
            {
                PatientMemberId = Guid.NewGuid().ToString(),
                PatientGroupNumber = Guid.NewGuid().ToString(),
            };

            var transportMessage = MakeTransportMessage(sample);
            var test = TransportMessageSerializerTests.Execute(transportMessage, serializer).Body.First() as Sample_Message;

            Assert.AreEqual(sample.PatientMemberId, test.PatientMemberId);
            Assert.AreEqual(sample.PatientGroupNumber, test.PatientGroupNumber);
        }

        [Test]
        public void TransportMessage_With_Null()
        {
            var sample = new Sample_Message()
            {
                PatientMemberId = Guid.NewGuid().ToString(),
                PatientGroupNumber = null,
            };

            var transportMessage = MakeTransportMessage(sample);
            var test = TransportMessageSerializerTests.Execute(transportMessage, serializer).Body.First() as Sample_Message;

            Assert.AreEqual(sample.PatientMemberId, test.PatientMemberId);
            Assert.Null(test.PatientGroupNumber);
        }

        [Test]
        public void TransportMessage_With_Empty()
        {
            var sample = new Sample_Message()
            {
                PatientMemberId = Guid.NewGuid().ToString(),
                PatientGroupNumber = string.Empty,
            };

            var transportMessage = MakeTransportMessage(sample);
            var test = TransportMessageSerializerTests.Execute(transportMessage, serializer).Body.First() as Sample_Message;

            Assert.AreEqual(sample.PatientMemberId, test.PatientMemberId);
            Assert.AreEqual(string.Empty, test.PatientGroupNumber);
        }

        private TransportMessage MakeTransportMessage(params IMessage[] input)
        {
            var transportMessage = new TransportMessage();
            transportMessage.Id = Guid.NewGuid().ToString();
            transportMessage.IdForCorrelation = Guid.NewGuid().ToString();
            transportMessage.ReturnAddress = Guid.NewGuid().ToString();
            transportMessage.WindowsIdentityName = string.Empty;
            transportMessage.TimeSent = DateTime.Now;
            transportMessage.Headers = new List<HeaderInfo>();
            transportMessage.Body = input;
            return transportMessage;
        }

        private IMessage SerializeThenDeserialize(IMessage input)
        {
            byte[] buffer = null;
            using (var stream = new MemoryStream())
            {
                serializer.Serialize(new[] { input }, stream);
                buffer = stream.ToArray();
            }

            using (var stream = new MemoryStream(buffer))
            {
                return serializer.Deserialize(stream).Cast<IMessage>().Single();
            }
        }
    }

    [Serializable]
    public class Sample_Message : IMessage
    {
        public string PatientMemberId { get; set; }
        public string PatientGroupNumber { get; set; }
    }
}
