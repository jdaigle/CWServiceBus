using System;

namespace CWServiceBus.SqlServer
{
    public static class ConfigurationExtensions
    {
        public static MessageBusBuilder UseSqlServerTransport(this MessageBusBuilder builder, Action<SqlServerTransportBuilder> configure)
        {
            builder.TransportBuilder = new SqlServerTransportBuilder(builder);
            if (configure != null)
                configure(builder.TransportBuilder as SqlServerTransportBuilder);
            return builder;
        }
    }
}
