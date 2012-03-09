using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CWServiceBus.Dispatch.TestData {
    public class MessageHandler3 : IMessageHandler<MessageWithInterface>, IMessageHandler<MessageWithoutInterface> {
        public void Handle(MessageWithInterface message) {
        }

        public void Handle(MessageWithoutInterface message) {
        }
    }
}
