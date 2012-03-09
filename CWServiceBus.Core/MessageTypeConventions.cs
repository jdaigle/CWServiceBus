using System;
using System.Collections.Generic;
using System.Linq;

namespace CWServiceBus {
    public class MessageTypeConventions {

        public static readonly MessageTypeConventions Default = new MessageTypeConventions();

        private List<Func<Type, bool>> isMessageType = new List<Func<Type, bool>>();

        public MessageTypeConventions() {
            isMessageType.Add(x => typeof(IMessage).IsAssignableFrom(x));
        }

        public MessageTypeConventions(IEnumerable<Func<Type, bool>> conventions)
            : this() {
            isMessageType.AddRange(conventions);
        }

        public void AddConvention(Func<Type, bool> convention) {
            this.isMessageType.Add(convention);
        }

        public void AddConventions(IEnumerable<Func<Type, bool>> conventions) {
            this.isMessageType.AddRange(conventions);
        }

        public bool IsMessageType(Type type) {
            return this.isMessageType.Any(x => x(type));
        }
    }
}
