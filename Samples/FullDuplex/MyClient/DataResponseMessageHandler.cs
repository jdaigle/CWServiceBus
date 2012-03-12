using System;
using MyMessages;
using CWServiceBus;

namespace MyClient
{
    class DataResponseMessageHandler : IMessageHandler<DataResponseMessage>
    {
        public void Handle(DataResponseMessage message)
        {
            Console.WriteLine("Response received with description: {0}", message.String);
        }
    }
}
