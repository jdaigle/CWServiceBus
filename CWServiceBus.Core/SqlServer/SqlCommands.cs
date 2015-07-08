using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CWServiceBus.SqlServer
{
    public static class SqlCommands
    {
        public const string CreateQueueTable = @"
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[{0}]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[{0}] (
        [RowId] [bigint] IDENTITY(1,1) NOT NULL,
        [Id] [uniqueidentifier] NOT NULL,
        [CorrelationId] [varchar](255) NULL,
        [ReplyToAddress] [varchar](255) NULL,
        [MessageIntent] [tinyint] NOT NULL,
        [Headers] [nvarchar](max) NOT NULL,
        [Body] [nvarchar](max) NULL
    );

    CREATE UNIQUE CLUSTERED INDEX [Index_Clustered_{0}] ON [dbo].[{0}] 
    (
        [RowId] ASC
    );
END
";

        public const string InsertMessage = @"
INSERT INTO [dbo].[{0}]
([Id], [CorrelationId], [ReplyToAddress], [MessageIntent], [Headers], [Body])
VALUES
(@Id, @CorrelationId, @ReplyToAddress, @MessageIntent, @Headers, @Body);
";

        public const string SelectMessage = @"
WITH message AS (SELECT TOP(1) * FROM [dbo].[{0}] WITH (UPDLOCK, READPAST, ROWLOCK) ORDER BY [RowId] ASC) 
DELETE FROM message 
OUTPUT
    deleted.Id,
    deleted.CorrelationId,
    deleted.ReplyToAddress, 
    deleted.MessageIntent,
    deleted.Headers,
    deleted.Body;";

        public const string InsertPoisonMessage = @"
INSERT INTO [dbo].[PoisonMessageQueue]
([Queue], [InsertDateTime], [Id], [CorrelationId], [ReplyToAddress], [MessageIntent], [Headers], [Body], [Exception])
VALUES
(@Queue, @InsertDateTime, @Id, @CorrelationId, @ReplyToAddress, @MessageIntent, @Headers, @Body, @Exception);
";
    }
}
