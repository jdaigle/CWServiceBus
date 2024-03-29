﻿using System;
using System.Collections.Generic;

namespace CWServiceBus {
    public interface IMessageDispatcher {
        /// <summary>
        /// Instance of the parent ServiceLocator
        /// </summary>
        IServiceLocator ServiceLocator { get; }

        void DispatchMessages(IServiceLocator childServiceLocator, IEnumerable<object> messages, IMessageContext messageContext);
        /// <summary>
        /// Fired before the the message is dispatched to message handlers
        /// </summary>
        event EventHandler<MessageDispatcherEventArgs> Dispatching;
        /// <summary>
        /// Fired after messages have been dispatched *even when an error occurs*
        /// </summary>
        event EventHandler<MessageDispatcherEventArgs> Dispatched;
        /// <summary>
        /// Fired when an error occurs dispatching messages
        /// </summary>
        event EventHandler<MessageDispatcherEventArgs> DispatchException;
    }
}
