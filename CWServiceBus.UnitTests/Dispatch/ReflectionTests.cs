using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using System.Reflection;

namespace CWServiceBus.Dispatch {
    [TestFixture]
    public class ReflectionTests {
        public interface InterfaceMessage {}
        public interface InterfaceMessage2 {}
        public class SubClassMessage : InterfaceMessage{}
        public class MessageHandler : IMessageHandler<InterfaceMessage> {
            public void Handle(InterfaceMessage message) {
                throw new NotImplementedException();
            }
        }

        [Test]
        public void Can_Find_Method_For_Interface() {
            Assert.NotNull(GetHandleMethod(typeof(MessageHandler), typeof(InterfaceMessage)));
        }

        [Test]
        public void Can_Find_Method_For_Subclass() {
            Assert.NotNull(GetHandleMethod(typeof(MessageHandler), typeof(SubClassMessage)));
        }

        static MethodInfo GetHandleMethod(Type targetType, Type messageType) {
            var method = targetType.GetMethod("Handle", new[] { messageType });
            if (method != null) return method;

            var handlerType = typeof(IMessageHandler<>).MakeGenericType(messageType);
            return targetType.GetInterfaceMap(handlerType)
                .TargetMethods
                .FirstOrDefault();
        }
    }
}
