using System;
using System.Collections.Generic;

namespace CWServiceBus {
    public class MessageDispatcherEventArgs : EventArgs {
        public IEnumerable<object> Messages { get; set; }
        /// <summary>
        /// Only Valid for "Dispatched" event.
        /// </summary>
        public bool DispatchedWithError { get; set; }
        /// <summary>
        /// Only Valid for "DispatchException" event
        /// </summary>
        public Exception DispatchException { get; set; }
    }
}
