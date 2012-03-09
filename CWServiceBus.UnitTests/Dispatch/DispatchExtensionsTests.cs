using CWServiceBus.Dispatch.TestData;
using NUnit.Framework;

namespace CWServiceBus.Dispatch {
    [TestFixture]
    public class DispatchExtensionsTests {
        [Test]
        public void IsMessageHandlerClassType_DetectsInterface() {
            Assert.True(typeof(MessageHandler1).IsMessageHandlerClassType());
            Assert.True(typeof(MessageHandler2).IsMessageHandlerClassType());
            Assert.True(typeof(MessageHandler3).IsMessageHandlerClassType());
        }

        [Test]
        public void IsMessageHandlerClassTypeForMessageType_DetectsInterfaceWithType() {
            Assert.True(typeof(MessageHandler1).IsMessageHandlerClassTypeForMessageType(typeof(MessageWithInterface)));
            Assert.True(typeof(MessageHandler2).IsMessageHandlerClassTypeForMessageType(typeof(MessageWithoutInterface)));
            Assert.True(typeof(MessageHandler3).IsMessageHandlerClassTypeForMessageType(typeof(MessageWithInterface)));
            Assert.True(typeof(MessageHandler3).IsMessageHandlerClassTypeForMessageType(typeof(MessageWithoutInterface)));
        }
    }
}
