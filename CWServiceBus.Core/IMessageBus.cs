using System;
using System.Collections.Generic;

namespace CWServiceBus
{
    /// <summary>
    /// Defines a Bus which can Send/Receive/Publish Messages
    /// </summary>
    public interface IMessageBus : IMessageCreator
    {
        void Send(params object[] messages);
        void Send<T>(Action<T> messageConstructor);
        void Send(string destinationService, params object[] messages);
        void Send<T>(string destinationService, Action<T> messageConstructor);
        void Send(IEnumerable<string> destinations, params object[] messages);
        void Send<T>(IEnumerable<string> destinations, Action<T> messageConstructor);

        void SendLocal(params object[] messages);
        void SendLocal<T>(Action<T> messageConstructor);

        /// <summary>
        /// Gets the list of key/value pairs that will be in the header of
        /// messages being sent by the same thread.
        /// 
        /// This value will be cleared when a thread receives a message.
        /// </summary>
        IDictionary<string, string> OutgoingHeaders { get; }

        /// <summary>
        /// Sends all messages to the endpoint which sent the message currently being handled on this thread.
        /// </summary>
        /// <param name="messages">The messages to send.</param>
        void Reply(params object[] messages);
        /// <summary>
        /// Sends all messages to the endpoint which sent the message currently being handled on this thread.
        /// </summary>
        void Reply<T>(Action<T> messageConstructor);

        /// <summary>
        /// Moves the message being handled to the back of the list of available 
        /// messages so it can be handled later.
        /// </summary>
        void HandleCurrentMessageLater();

        /// <summary>
        /// Forwards the current message being handled to the destination maintaining
        /// all of its transport-level properties and headers.
        /// </summary>
        /// <param name="destination"></param>
        void ForwardCurrentMessageTo(string destination);

        /// <summary>
        /// Tells the bus to stop dispatching the current message to additional
        /// handlers.
        /// </summary>
        void DoNotContinueDispatchingCurrentMessageToHandlers();

        /// <summary>
        /// Gets the message context containing the Id, return address, and headers
        /// of the message currently being handled on this thread.
        /// </summary>
        IMessageContext CurrentMessageContext { get; }
    }
}
