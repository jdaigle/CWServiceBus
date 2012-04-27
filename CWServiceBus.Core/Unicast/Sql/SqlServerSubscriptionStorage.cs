using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using Rhino.Commons;

namespace CWServiceBus.Unicast.Sql
{
    public class SqlServerSubscriptionStorage : ISubscriptionStorage
    {
        public ISubscriptionCacheProvider SubscriptionCacheProvider { get; set; }
        public string ConnectionString { get; set; }

        public void Subscribe(string destinationService, IEnumerable<MessageType> messageTypes)
        {
            using (var connection = new SqlConnection(this.ConnectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    using (var commandSet = new SqlCommandSet())
                    {
                        commandSet.Connection = connection;
                        commandSet.Transaction = transaction;
                        foreach (var messageType in messageTypes)
                        {
                            var cmd = connection.CreateCommand();
                            cmd.CommandText = "IF (NOT EXISTS (SELECT 1 FROM Subscription WHERE ServiceName = @serviceName AND MessageType = @messageType)) INSERT INTO Subscription ([ServiceName], [MessageType]) VALUES (@serviceName, @messageType);";
                            cmd.Parameters.AddWithValue("@serviceName", destinationService);
                            cmd.Parameters.AddWithValue("@messageType", messageType.TypeName);
                            cmd.Transaction = transaction;
                            commandSet.Append(cmd);
                        }
                        commandSet.ExecuteNonQuery();
                    }
                    transaction.Commit();
                }
            }
            if (SubscriptionCacheProvider != null)
            {
                SubscriptionCacheProvider.Clear(messageTypes);
            }
        }

        public void Unsubscribe(string destinationService, IEnumerable<MessageType> messageTypes)
        {
            using (var connection = new SqlConnection(this.ConnectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    using (var commandSet = new SqlCommandSet())
                    {
                        commandSet.Connection = connection;
                        commandSet.Transaction = transaction;
                        foreach (var messageType in messageTypes)
                        {
                            var cmd = connection.CreateCommand();
                            cmd.CommandText = "DELETE FROM Subscription WHERE ServiceName = @serviceName AND MessageType = @messageType;";
                            cmd.Parameters.AddWithValue("@serviceName", destinationService);
                            cmd.Parameters.AddWithValue("@messageType", messageType.TypeName);
                            cmd.Transaction = transaction;
                            commandSet.Append(cmd);
                        }
                        commandSet.ExecuteNonQuery();
                    }
                    transaction.Commit();
                }
            }
            if (SubscriptionCacheProvider != null)
            {
                SubscriptionCacheProvider.Clear(messageTypes);
            }
        }

        public IEnumerable<string> GetSubscriberServicesForMessage(IEnumerable<MessageType> messageTypes)
        {
            var subscribers = new List<string>();

            if (!messageTypes.Any())
            {
                return subscribers;
            }

            if (SubscriptionCacheProvider != null && SubscriptionCacheProvider.Get(messageTypes, out subscribers))
            {
                return subscribers;
            }

            subscribers = subscribers ?? new List<string>();

            using (var connection = new SqlConnection(this.ConnectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    var messageTypesString = new StringBuilder();
                    for (int i = 0; i < messageTypes.Count(); i++)
                    {
                        messageTypesString.AppendFormat("@p{0},", i);
                        command.Parameters.AddWithValue("@p" + i.ToString(), messageTypes.ElementAt(i).TypeName);
                    }
                    command.CommandText = string.Format("SELECT [ServiceName] FROM [Subscription] WHERE [MessageType] IN ({0})", messageTypesString.ToString().TrimEnd(','));
                    using (var transaction = connection.BeginTransaction())
                    {
                        command.Transaction = transaction;
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                subscribers.Add(reader.GetString(0));
                            }
                        }
                        transaction.Commit();
                    }
                }
            }

            if (SubscriptionCacheProvider != null)
            {
                SubscriptionCacheProvider.Set(messageTypes, subscribers);
            }

            return subscribers;
        }
    }
}
