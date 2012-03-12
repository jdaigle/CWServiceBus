using System.Linq;
using CWServiceBus.Dispatch.TestData;
using NUnit.Framework;

namespace CWServiceBus.Dispatch {
    [TestFixture]
    public class MessageHandlerCollectionTests {

        private MessageHandlerCollection messageHandlerCollection;

        [TestFixtureSetUp]
        public void RegisterAssemblyMessageHandlers() {
            var conventions = new MessageTypeConventions();
            conventions.AddConvention(x => x.IsInterface && x.Namespace == "CWServiceBus.Dispatch.TestData");
            messageHandlerCollection = new MessageHandlerCollection(conventions);
            messageHandlerCollection.AddAssemblyToScan(GetType().Assembly);
            messageHandlerCollection.Init();
        }

        [Test]
        public void CanRegisterWithoutFailure() {
            Assert.Pass();
        }

        [Test]
        public void Supports_Conventions() {
            var conventions = new MessageTypeConventions();
            conventions.AddConvention(x => x.Name.EndsWith("MessageWithoutHandler"));
            messageHandlerCollection = new MessageHandlerCollection(conventions);
            messageHandlerCollection.AddAssemblyToScan(GetType().Assembly);
            messageHandlerCollection.Init();
            messageHandlerCollection.AllMessageTypes().Contains(typeof(MessageWithoutHandler));
            Assert.IsEmpty(messageHandlerCollection.GetOrderedDispatchInfoFor(typeof(MessageWithoutHandler)));
        }

        [Test]
        public void Can_Find_Basic_MessageHandlers() {
            var handlers = messageHandlerCollection.GetOrderedDispatchInfoFor(typeof(MessageWithInterface)).ToList();
            Assert.AreEqual(2, handlers.Count());
            Assert.AreEqual(1, handlers.Count(x => x.InstanceType == typeof(MessageHandler1)));
            Assert.AreEqual(1, handlers.Count(x => x.InstanceType == typeof(MessageHandler3)));

            handlers = messageHandlerCollection.GetOrderedDispatchInfoFor(typeof(MessageWithoutInterface)).ToList();
            Assert.AreEqual(2, handlers.Count());
            Assert.AreEqual(1, handlers.Count(x => x.InstanceType == typeof(MessageHandler2)));
            Assert.AreEqual(1, handlers.Count(x => x.InstanceType == typeof(MessageHandler3)));
        }

        [Test]
        public void Can_Find_MessageHandler_For_ChildType() {
            var handlers = messageHandlerCollection.GetOrderedDispatchInfoFor(typeof(MessageWithCustomInterface)).ToList();
            Assert.AreEqual(2, handlers.Count());
            Assert.AreEqual(1, handlers.Count(x => x.InstanceType == typeof(MessageHandler4)));
            Assert.AreEqual(1, handlers.Count(x => x.InstanceType == typeof(MessageHandler5)));
            var methodInfo = handlers.First(x => x.InstanceType == typeof(MessageHandler5)).MethodInfo;

            handlers = messageHandlerCollection.GetOrderedDispatchInfoFor(typeof(ICustomMessage)).ToList();
            Assert.AreEqual(1, handlers.Count());
            Assert.AreEqual(1, handlers.Count(x => x.InstanceType == typeof(MessageHandler5)));
            Assert.AreEqual(1, handlers.Count(x => x.MessageType == typeof(ICustomMessage)));
            Assert.AreEqual(methodInfo, handlers.First().MethodInfo);
        }

        [Test]
        public void Can_Order_Handlers() {
            messageHandlerCollection.ExecuteTheseHandlersFirst(typeof(MessageHandler3));
            Assert.AreEqual(typeof(MessageHandler3), messageHandlerCollection.GetOrderedDispatchInfoFor(typeof(MessageWithInterface)).First().InstanceType);

            messageHandlerCollection.ExecuteTheseHandlersFirst(typeof(MessageHandler1));
            Assert.AreEqual(typeof(MessageHandler1), messageHandlerCollection.GetOrderedDispatchInfoFor(typeof(MessageWithInterface)).First().InstanceType);
        }
    }
}
