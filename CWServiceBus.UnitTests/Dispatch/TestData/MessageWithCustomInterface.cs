using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CWServiceBus.Dispatch.TestData {
    public class MessageWithCustomInterface : ICustomMessage {
        public string Data { get; set; }
    }
}
