using System;
using System.Collections.Generic;
using CWServiceBus.Transport;

namespace CWServiceBus.Diagnostics
{
    public class PerformanceCounters
    {
        public void OnMessageReceived(ITransport transport)
        {
            Metrics.Default.Increment("CWServiceBus.MessageReceived." + transport.ListenerQueue);
        }

        public void OnMessageSent(ITransport transport, IEnumerable<string> destinations, IEnumerable<object> messages)
        {
            foreach (var dest in destinations)
            {
                foreach (var msg in messages)
                {
                    Metrics.Default.SendBatch()
                        .Increment("CWServiceBus.MessageSent.Source." + transport.ReturnAddress)
                        .Increment("CWServiceBus.MessageSent.Destination." + dest)
                        .Increment("CWServiceBus.MessageSent.Message." + msg.GetType().Name)
                        .Send();
                }
            }
        }

        public void OnMessageFailure(ITransport transport)
        {
            Metrics.Default.Increment("CWServiceBus.MessageFailure.Destination." + transport.ListenerQueue);
        }

        public void OnMessageHandled(ITransport transport, TransportMessage transportMessage, double elapsedMilliseconds)
        {
            var tr = (long)Math.Round(elapsedMilliseconds);
            Metrics.Default.SendBatch()
                        .AddTiming("CWServiceBus.MessageHandled.Destination." + transport.ListenerQueue, tr)
                        .AddTiming("CWServiceBus.MessageHandled.Message." + transportMessage.Body[0].GetType().Name, tr)
                        .Send();
        }
    }
}