using System;
using System.Data.SqlClient;

namespace CWServiceBus.ServiceBroker {
    public interface ISqlServerTransactionWrapper {
        void RunInTransaction(Action<SqlTransaction> callback);
    }
}