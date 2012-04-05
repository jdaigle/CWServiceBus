using System;

namespace CWServiceBus.Unicast
{
    public class MessageHandledEventArgs : EventArgs
    {
        public long ElapsedMilliseconds { get; set; }
        public long ElapsedTicks { get; set; }
    }
}
