namespace CWServiceBus.CommandBus.Messages {

    public interface ICommand : IMessage {
    }

    public class Command1 : ICommand {
        public string Data { get; set; }
    }

    public class Command2 : ICommand {
        public string Data { get; set; }
    }

    public interface ICommand3 : ICommand {
        string Data { get; set; }
    }

    public class Command4 : ICommand {
        public string Data { get; set; }
    }
}
