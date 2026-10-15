using AiNexus.Platform.Data.Sql;

namespace AiNexus.Features.Integrations.Sources;

/// <summary>The read-only Meiho (school administration) source database, settings <c>Databases:Meiho</c>.</summary>
public sealed class MeihoDatabase : ISqlDatabaseDefinition
{
    private MeihoDatabase() { }

    public static string Name => "Meiho";
    public static bool ReadOnly => true;
}
