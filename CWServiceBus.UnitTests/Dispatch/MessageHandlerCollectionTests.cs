using System.Linq;
using CWServiceBus.Dispatch.TestData;
using NUnit.Framework;

namespace CWServiceBus.Dispatch {
    [TestFixture]
    public class MessageHandlerCollectionTests {

        private MessageHandlerCollection messageHandlerCollection;

        [TestFixtureSetUp]
        public void RegisterAssemblyMessageHandlers() {
            messageHandlerCollection = new MessageHandlerCollection();
            messageHandlerCollection.AddAssemblyToScan(GetType().Assembly);
            messageHandlerCollection.AddMessageTypeConvention(x => x.IsInterface && x.Namespace == "CWServiceBus.Dispatch.TestData");
            messageHandlerCollection.Init();
        }

        [Test]
        public void CanRegisterWithoutFailure() {
            Assert.Pass();
        }

        [Test]
        public void Supports_Conventions() {
            messageHandlerCollection = new MessageHandlerCollection();
            messageHandlerCollection.AddAssemblyToScan(GetType().Assembly);
            messageHandlerCollection.AddMessageTypeConvention(x => x.Name.EndsWith("MessageWithoutHandler"));
            messageHandlerCollection.Init();
            messageHandlerCollection.AllMessageTypes().Contains(typeof(MessageWithoutHandler));
            Assert.IsEmpty(messageHandlerCollection.GetOrderedHandlersFor(typeof(MessageWithoutHandler)));
        }

        [Test]
        public void Can_Find_Basic_MessageHandlers() {
            var handlers = messageHandlerCollection.GetOrderedHandlersFor(typeof(MessageWithInterface));
            Assert.AreEqual(2, handlers.Count());
            Assert.AreEqual(1, handlers.Count(x => x.InstanceType == typeof(MessageHandler1)));
            Assert.AreEqual(1, handlers.Count(x => x.InstanceType == typeof(MessageHandler3)));

            handlers = messageHandlerCollection.GetOrderedHandlersFor(typeof(MessageWithoutInterface));
            Assert.AreEqual(2, handlers.Count());
            Assert.AreEqual(1, handlers.Count(x => x.InstanceType == typeof(MessageHandler2)));
            Assert.AreEqual(1, handlers.Count(x => x.InstanceType == typeof(MessageHandler3)));
        }

        [Test]
        public void Can_Find_MessageHandler_For_ParentType() {
            var handlers = messageHandlerCollection.GetOrderedHandlersFor(typeof(MessageWithCustomInterface));
            Assert.AreEqual(1, handlers.Count());
            Assert.AreEqual(1, handlers.Count(x => x.InstanceType == typeof(MessageHandler4)));
            Assert.AreEqual(1, handlers.Count(x => x.MessageType == typeof(MessageWithCustomInterface)));
            var methodInfo = handlers.First().MethodInfo;

            handlers = messageHandlerCollection.GetOrderedHandlersFor(typeof(ICustomMessage));
            Assert.AreEqual(1, handlers.Count());
            Assert.AreEqual(1, handlers.Count(x => x.InstanceType == typeof(MessageHandler4)));
            Assert.AreEqual(1, handlers.Count(x => x.MessageType == typeof(ICustomMessage)));
            Assert.AreEqual(methodInfo, handlers.First().MethodInfo);
        }

        [Test]
        public void Can_Order_Handlers() {
            messageHandlerCollection.ExecuteTheseHandlersFirst(typeof(MessageHandler3));
            Assert.AreEqual(typeof(MessageHandler3), messageHandlerCollection.GetOrderedHandlersFor(typeof(MessageWithInterface)).First().InstanceType);

            messageHandlerCollection.ExecuteTheseHandlersFirst(typeof(MessageHandler1));
            Assert.AreEqual(typeof(MessageHandler1), messageHandlerCollection.GetOrderedHandlersFor(typeof(MessageWithInterface)).First().InstanceType);
        }
    }
}
