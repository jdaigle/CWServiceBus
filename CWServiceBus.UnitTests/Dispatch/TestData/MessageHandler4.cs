using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CWServiceBus.Dispatch.TestData {
    public class MessageHandler4 : IMessageHandler<MessageWithCustomInterface> {
        public void Handle(MessageWithCustomInterface message) {
        }
    }
}
