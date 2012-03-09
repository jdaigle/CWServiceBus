using System;
using System.Collections.Generic;

namespace CWServiceBus
{
    public interface IMessageContext
    {
        /// <summary>
        /// Id of the current message being handled.
        /// </summary>
        Guid MessageId { get; }

        /// <summary>
        /// The address of the endpoint that sent the current message being handled.
        /// </summary>
        string ReplyToService { get; }

        /// <summary>
        /// Returns the time at which the message was sent.
        /// </summary>
        DateTime TimeSent { get; }

        /// <summary>
        /// Gets the list of key/value pairs found in the header of the message.
        /// </summary>
        IDictionary<string, string> Headers { get; }
    }
}
