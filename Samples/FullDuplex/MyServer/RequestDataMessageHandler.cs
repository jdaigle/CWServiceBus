using System.Threading;
using CWServiceBus;
using log4net;
using MyMessages;

namespace MyServer {
    public class RequestDataMessageHandler : IMessageHandler<RequestDataMessage> {
        public IMessageBus MessageBus { get; set; }

        public void Handle(RequestDataMessage message) {
            //try to uncomment the line below to see the error handling in action
            // 1. nservicebus will retry the configured number of times configured in app.config
            // 2. the UoW will rollback
            // 3. When the max retries is reached the message will be given to the faultmanager (in memory in this case)
            //throw new Exception("Database connection lost");

            Logger.Info("==========================================================================");
            Logger.InfoFormat("Received request {0}.", message.DataId);
            Logger.InfoFormat("String received: {0}.", message.String);
            Logger.InfoFormat("Header 'Test' = {0}.", MessageBus.GetHeader("Test"));
            Logger.InfoFormat(Thread.CurrentPrincipal != null ? Thread.CurrentPrincipal.Identity.Name : string.Empty);

            var response = MessageBus.CreateInstance<DataResponseMessage>(m => {
                m.DataId = message.DataId;
                m.String = message.String;
            });


            MessageBus.CopyHeaderFromRequest();
            MessageBus.SetHeader("1", "1");
            MessageBus.SetHeader("2", "2");

            MessageBus.Reply(response);

            Thread.Sleep(250);
        }

        public static ILog Logger = LogManager.GetLogger("MyServer");
    }
}
