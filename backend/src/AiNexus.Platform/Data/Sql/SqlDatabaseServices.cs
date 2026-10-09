using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AiNexus.Platform.Data.Sql;

public static class SqlDatabaseServices
{
    /// <summary>Registers <see cref="ISqlDatabase{TDatabase}"/> for a database outside EF Core.</summary>
    public static IServiceCollection AddSqlDatabase<TDatabase>(this IServiceCollection services) where TDatabase : ISqlDatabaseDefinition
    {
        services.TryAddSingleton<ISqlDatabase<TDatabase>, SqlDatabase<TDatabase>>();
        return services;
    }

    /// <summary>Registers <see cref="ISqlDatabase{TDatabase}"/> on the connection of a registered DbContext.</summary>
    public static IServiceCollection AddDbContextSqlDatabase<TContext>(this IServiceCollection services) where TContext : DbContext
    {
        services.TryAddScoped<ISqlDatabase<TContext>, DbContextSqlDatabase<TContext>>();
        return services;
    }
}
