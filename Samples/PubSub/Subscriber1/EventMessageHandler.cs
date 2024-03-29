using System;
using CWServiceBus;
using log4net;
using MyMessages;

namespace Subscriber1 {
    public class EventMessageHandler : IMessageHandler<EventMessage> {
        public void Handle(EventMessage message) {
            Logger.Info(string.Format("Subscriber 1 received EventMessage with Id {0}.", message.EventId));
            Logger.Info(string.Format("Message time: {0}.", message.Time));
            Logger.Info(string.Format("Message duration: {0}.", message.Duration));
            Console.WriteLine("==========================================================================");
        }

        private static readonly ILog Logger = LogManager.GetLogger(typeof(EventMessageHandler));
    }
}
