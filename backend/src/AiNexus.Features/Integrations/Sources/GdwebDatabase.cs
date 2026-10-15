using AiNexus.Platform.Data.Sql;

namespace AiNexus.Features.Integrations.Sources;

/// <summary>The read-only Gdweb (official documents) source database, settings <c>Databases:Gdweb</c>.</summary>
public sealed class GdwebDatabase : ISqlDatabaseDefinition
{
    private GdwebDatabase() { }

    public static string Name => "Gdweb";
    public static bool ReadOnly => true;
}
