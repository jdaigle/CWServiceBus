using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using System.Text;
using CWServiceBus.Serializers.XML.NS1;
using CWServiceBus.Serializers.XML.NS2;
using CWServiceBus.Reflection;

namespace CWServiceBus.Serializers.XML {
    [TestFixture]
    public class NamespaceTests {
        private int numberOfIterations = 100;

        [Test]
        public void Serialize_Deserialize_Multiple_Namespaces() {
            var types = new List<Type> { typeof(C1), typeof(C2) };
            var mapper = new MessageMapper();
            mapper.Initialize(types);
            var serializer = new XmlMessageSerializer(mapper);

            serializer.Initialize(types);

            Time(new IMessage[] { new C1() { Data = "o'tool" }, new C2() { Data = "Timmy" } }, serializer);
        }

        private void Time(IMessage[] messages, IMessageSerializer serializer) {
            Stopwatch watch = new Stopwatch();
            watch.Start();

            for (int i = 0; i < numberOfIterations; i++)
                using (MemoryStream stream = new MemoryStream())
                    serializer.Serialize(messages, stream);

            watch.Stop();
            Debug.WriteLine("Serializing: " + watch.Elapsed);

            watch.Reset();

            MemoryStream s = new MemoryStream();
            serializer.Serialize(messages, s);
            byte[] buffer = s.GetBuffer();
            s.Dispose();
            Console.WriteLine(Encoding.ASCII.GetString(buffer));

            watch.Start();

            object[] result = null;

            for (int i = 0; i < numberOfIterations; i++)
                using (var forDeserializing = new MemoryStream(buffer))
                    result = serializer.Deserialize(forDeserializing);

            watch.Stop();
            Debug.WriteLine("Deserializing: " + watch.Elapsed);
        }
    }
}
