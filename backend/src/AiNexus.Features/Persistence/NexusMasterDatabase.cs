using AiNexus.Platform.Data.Sql;

namespace AiNexus.Features.Persistence;

/// <summary>The master database of the Nexus server, used only to create the AiNexus database before migrations run.</summary>
public sealed class NexusMasterDatabase : ISqlDatabaseDefinition
{
    private NexusMasterDatabase() { }

    public static string Name => NexusDbContext.DatabaseName;
    public static string? Catalog => "master";
}
