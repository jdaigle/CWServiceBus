using System;
using System.Data.SqlClient;
using log4net;

namespace CWServiceBus.SqlServer
{
    public class SqlServerTransactionWrapper : ISqlServerTransactionWrapper
    {
        [ThreadStatic]
        private static int loopLevel = 0;
        [ThreadStatic]
        private static SqlConnection connection;
        [ThreadStatic]
        private static SqlTransaction transaction;
        [ThreadStatic]
        private static Guid transactionId;

        internal SqlServerTransactionWrapper(string connectionString)
        {
            this.connectionString = connectionString;
        }

        private string connectionString;
        public static ILog Logger = LogManager.GetLogger(typeof(SqlServerTransactionWrapper));

        public void RunInTransaction(Action<SqlTransaction> callback, bool skipOpenConnection = false)
        {
            bool isTopLevel = loopLevel == 0;
            loopLevel++;
            try
            {
                if (connection == null && !skipOpenConnection)
                {
                    connection = new SqlConnection(connectionString);
                    connection.Open();
                }

                if (transaction == null && connection != null && !skipOpenConnection)
                {
                    transactionId = Guid.NewGuid();
                    Logger.Debug(string.Format("Beginning Transaction [{0}]", transactionId));
                    transaction = connection.BeginTransaction();
                }

                // The callback might rollback the transaction, we always commit it
                callback(transaction);

                if (isTopLevel && transaction != null)
                {
                    // We always commit our transactions, the callback might roll it back though
                    Logger.Debug(string.Format("Committing Transaction [{0}]", transactionId));
                    transaction.Commit();
                }
            }
            catch (Exception e)
            {
                if (isTopLevel && transaction != null)
                {
                    Logger.Debug(string.Format("Rolling Back Transaction with Error [{0}]", transactionId), e);
                    transaction.Rollback();
                }
                throw;
            }
            finally
            {
                loopLevel--; // it is important we decrement the loopLevel first in the finally {} block so that
                             // we start with a clean slate on the next message we process on this thread, regardless
                             // of whether the code below throws an new exception or not.
                if (isTopLevel && transaction != null)
                {
                    if (transaction != null)
                    {
                        transaction.Dispose();
                    }
                    transaction = null;
                }

                if (isTopLevel && connection != null)
                {
                    try
                    {
                        connection.Close();
                        connection.Dispose();
                    }
                    finally
                    {
                        connection = null;
                    }
                }
            }

        }
    }
}
