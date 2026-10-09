using AiNexus.Platform.Data.Sql;

namespace AiNexus.Features.Integrations;

/// <summary>The read-only Meiho (school administration) source database, connection string <c>LegacyMeiho</c>.</summary>
public sealed class LegacyMeihoDatabase : ISqlDatabaseDefinition
{
    private LegacyMeihoDatabase() { }

    public static string ConnectionString(IConfiguration configuration) => SourceConnection.ReadOnly(configuration, "meiho");
}
