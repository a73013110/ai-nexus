namespace AiNexus.Platform.Data.Sql;

/// <summary>
/// A SQL Server database outside the application DbContext (the server's master database, read-only external sources).
/// The owning module declares which <c>Databases</c> entry it connects with; <see cref="DatabaseOptions"/> builds the connection.
/// </summary>
public interface ISqlDatabaseDefinition
{
    /// <summary>The entry under <c>Databases</c> that holds the connection settings.</summary>
    static abstract string Name { get; }

    /// <summary>A database on the same server to open instead of the entry's <c>Name</c>; <see langword="null"/> keeps it.</summary>
    static virtual string? Catalog => null;

    /// <summary>The module only reads this database: its entry must set <c>ReadOnly=true</c>, or startup fails.</summary>
    static virtual bool ReadOnly => false;
}
