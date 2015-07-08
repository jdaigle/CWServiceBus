using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using CWServiceBus.Transport;

namespace CWServiceBus.SqlServer
{
    public class QueueNotFoundException : Exception
    {
        public TransportMessage TransportMessage { get; set; }

        public QueueNotFoundException(TransportMessage transportMessage, string message, SqlException innerException)
            : base(message, innerException)
        {
            this.TransportMessage = transportMessage;
        }
    }
}
