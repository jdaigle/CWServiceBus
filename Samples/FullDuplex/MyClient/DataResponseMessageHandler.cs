using System;
using CWServiceBus;
using MyMessages;

namespace MyClient {
    class DataResponseMessageHandler : IMessageHandler<DataResponseMessage> {
        public IMessageBus MessageBus { get; set; }

        public void Handle(DataResponseMessage message) {
            Console.WriteLine("Response received with description: {0}", message.String);

            Console.WriteLine("==========================================================================");
            Console.WriteLine(
                "Response with header 'Test' = {0}, 1 = {1}, 2 = {2}.",
                MessageBus.CurrentMessageContext.Headers["Test"],
                MessageBus.CurrentMessageContext.Headers["1"],
                MessageBus.CurrentMessageContext.Headers["2"]);
        }
    }
}
