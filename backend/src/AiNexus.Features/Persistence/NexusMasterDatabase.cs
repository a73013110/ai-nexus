using AiNexus.Platform.Data.Sql;
using Microsoft.Data.SqlClient;

namespace AiNexus.Features.Persistence;

/// <summary>The master database of the Nexus server, used only to create the AiNexus database before migrations run.</summary>
public sealed class NexusMasterDatabase : ISqlDatabaseDefinition
{
    private NexusMasterDatabase() { }

    public static string ConnectionString(IConfiguration configuration)
        => new SqlConnectionStringBuilder(configuration.GetConnectionString("Nexus") ?? "") { InitialCatalog = "master" }.ConnectionString;
}
