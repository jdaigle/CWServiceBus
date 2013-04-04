using System;
using System.Data.SqlClient;
using log4net;

namespace CWServiceBus.ServiceBroker
{
    public class SqlServerTransactionWrapper : ISqlServerTransactionWrapper
    {
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

        public void RunInTransaction(Action<SqlTransaction> callback)
        {
            bool closeConnection = connection == null;
            bool disposeTransaction = transaction == null;

            try
            {
                if (connection == null)
                {
                    connection = new SqlConnection(connectionString);
                    connection.Open();
                }

                if (transaction == null)
                {
                    transactionId = Guid.NewGuid();
                    Logger.Debug(string.Format("Beginning Transaction [{0}]", transactionId));
                    transaction = connection.BeginTransaction();
                }

                // The callback might rollback the transaction, we always commit it
                callback(transaction);

                if (disposeTransaction)
                {
                    // We always commit our transactions, the callback might roll it back though
                    Logger.Debug(string.Format("Committing Transaction [{0}]", transactionId));
                    transaction.Commit();
                }
            }
            catch (Exception e)
            {
                if (disposeTransaction && transaction != null)
                {
                    Logger.Debug(string.Format("Rolling Back Transaction with Error [{0}]", transactionId), e);
                    transaction.Rollback();
                }
                throw;
            }
            finally
            {
                if (disposeTransaction)
                {
                    if (transaction != null)
                    {
                        transaction.Dispose();
                    }
                    transaction = null;
                }

                if (closeConnection)
                {
                    if (connection != null)
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
}
