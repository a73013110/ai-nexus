using System.Data.Common;
using EDoc.Core.Database.Enums;
using EDoc.Core.Database.Interfaces;
using EDoc.Core.Database.Markers;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Database;

public interface INexusDatabase : IDbMarker
{
    static string IDbMarker.ConnectionStringKey => "Nexus";
    static DbProviderType IDbMarker.ProviderType => DbProviderType.SqlServer;
}

public interface INexusBootstrapDatabase : IDbMarker
{
    static string IDbMarker.ConnectionStringKey => "Nexus";
    static DbProviderType IDbMarker.ProviderType => DbProviderType.SqlServer;
}

public sealed class NexusConnectionFactory(IConfiguration configuration) : IDbConnectionFactory
{
    public DbConnection CreateConnection<TDb>() where TDb : IDbMarker
    {
        var connection = new SqlConnectionStringBuilder(ConnectionString<TDb>());
        if (typeof(TDb) == typeof(INexusBootstrapDatabase)) connection.InitialCatalog = "master";
        return new SqlConnection(connection.ConnectionString);
    }
    public DbContextOptions<TContext> CreateDbContextOptions<TDb, TContext>()
        where TDb : IDbMarker where TContext : DbContext
        => new DbContextOptionsBuilder<TContext>().UseSqlServer(ConnectionString<TDb>()).Options;

    private string ConnectionString<TDb>() where TDb : IDbMarker
        => configuration.GetConnectionString(TDb.ConnectionStringKey) ?? "";
}
