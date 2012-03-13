using System;
using CWServiceBus.CommandBus.Messages;

namespace CWServiceBus.CommandBus {
    public class CommandHandler1 : IMessageHandler<Command1> {
        public static Action<Command1> callback;
        public void Handle(Command1 message) {
            if (callback != null)
                callback(message);
        }
    }

    public class CommandHandler2 : IMessageHandler<Command2> {
        public static Action<Command2> callback;
        public void Handle(Command2 message) {
            if (callback != null)
                callback(message);
        }
    }

    public class CommandHandler3 : IMessageHandler<ICommand3> {
        public static Action<ICommand3> callback;
        public void Handle(ICommand3 message) {
            if (callback != null)
                callback(message);
        }
    }

    public class CommandHandler_Generic : IMessageHandler<ICommand> {
        public static Action<ICommand> callback;
        public void Handle(ICommand message) {
            if (callback != null)
                callback(message);
        }
    }

    public class CommandHandler4 : IMessageHandler<Command4> {
        public void Handle(Command4 message) {
            throw new ApplicationException("Rollback");
        }
    }
}
