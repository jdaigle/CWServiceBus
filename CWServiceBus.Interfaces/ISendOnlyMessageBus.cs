using System;
using System.Collections.Generic;

namespace CWServiceBus {
    public interface ISendOnlyMessageBus : IMessageCreator {
        void Send(params object[] messages);
        void Send<T>(Action<T> messageConstructor);
        void Send(string destinationService, params object[] messages);
        void Send<T>(string destinationService, Action<T> messageConstructor);
        void Send(IEnumerable<string> destinations, params object[] messages);
        void Send<T>(IEnumerable<string> destinations, Action<T> messageConstructor);

        /// <summary>
        /// Gets the list of key/value pairs that will be in the header of
        /// messages being sent by the same thread.
        /// 
        /// This value will be cleared when a thread receives a message.
        /// </summary>
        IDictionary<string, string> OutgoingHeaders { get; }
    }
}
