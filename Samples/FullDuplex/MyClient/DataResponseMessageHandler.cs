﻿using System;
using CWServiceBus;
using MyMessages;

namespace MyClient {
    class DataResponseMessageHandler : IMessageHandler<DataResponseMessage> {
        public IServiceBus ServiceBus { get; set; }

        public void Handle(DataResponseMessage message) {
            Console.WriteLine("Response received with description: {0}", message.String);

            Console.WriteLine("==========================================================================");
            Console.WriteLine(
                "Response with header 'Test' = {0}, 1 = {1}, 2 = {2}.",
                ServiceBus.CurrentMessageContext.Headers["Test"],
                ServiceBus.CurrentMessageContext.Headers["1"],
                ServiceBus.CurrentMessageContext.Headers["2"]);
        }
    }
}