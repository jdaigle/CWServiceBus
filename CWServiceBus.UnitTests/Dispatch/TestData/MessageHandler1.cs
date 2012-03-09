using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CWServiceBus.Dispatch.TestData {
    public class MessageHandler1 : IMessageHandler<MessageWithInterface> {
        public void Handle(MessageWithInterface message) {
        }
    }
}
