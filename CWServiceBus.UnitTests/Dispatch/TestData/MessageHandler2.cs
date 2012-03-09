using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CWServiceBus.Dispatch.TestData {
    public class MessageHandler2 : IMessageHandler<MessageWithoutInterface> {
        public void Handle(MessageWithoutInterface message) {
        }
    }
}
