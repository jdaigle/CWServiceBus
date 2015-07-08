using System;
using System.Data.SqlClient;

namespace CWServiceBus.SqlServer
{
    public interface ISqlServerTransactionWrapper
    {
        /// <summary>
        /// When skipOpenConnection = true, we won't open a connection.
        /// But nested calls to RunInTransaction might. But only the "outer" call
        /// to RunInTransaction will commit and close the connection.
        /// </summary>
        /// <param name="callback"></param>
        /// <param name="skipOpenConnection"></param>
        void RunInTransaction(Action<SqlTransaction> callback, bool skipOpenConnection = false);
    }
}