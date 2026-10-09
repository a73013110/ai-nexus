using AiNexus.Platform.Data.Sql;

namespace AiNexus.Features.Integrations;

/// <summary>The read-only Gdweb (official documents) source database, connection string <c>LegacyGdweb</c>.</summary>
public sealed class LegacyGdwebDatabase : ISqlDatabaseDefinition
{
    private LegacyGdwebDatabase() { }

    public static string ConnectionString(IConfiguration configuration) => SourceConnection.ReadOnly(configuration, "gdweb");
}
