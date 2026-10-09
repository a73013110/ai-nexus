namespace AiNexus.Platform.Data.Sql;

/// <summary>
/// A SQL Server database outside the application DbContext (the server's master database, read-only external sources).
/// The owning module declares it and decides its connection settings.
/// </summary>
public interface ISqlDatabaseDefinition
{
    /// <summary>The connection string, or an empty string when the database is not configured.</summary>
    static abstract string ConnectionString(IConfiguration configuration);
}
