using System;
using log4net;

namespace Subscriber2 {
    using CWServiceBus;
    using MyMessages;

    public class EventMessageHandler : IMessageHandler<IMyEvent> {
        public void Handle(IMyEvent message) {
            Logger.Info(string.Format("Subscriber 2 received IEvent with Id {0}.", message.EventId));
            Logger.Info(string.Format("Message time: {0}.", message.Time));
            Logger.Info(string.Format("Message duration: {0}.", message.Duration));
            Console.WriteLine("==========================================================================");
        }

        private static readonly ILog Logger = LogManager.GetLogger(typeof(EventMessageHandler));
    }
}
