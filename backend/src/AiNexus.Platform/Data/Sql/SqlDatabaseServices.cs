using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AiNexus.Platform.Data.Sql;

public static class SqlDatabaseServices
{
    /// <summary>
    /// Declares that a module connects with <c>Databases:&lt;name&gt;</c>. Only declared entries pass validation;
    /// <paramref name="readOnly"/> also requires the entry to set <c>ReadOnly=true</c>.
    /// </summary>
    public static IServiceCollection AddDatabase(this IServiceCollection services, string name, bool readOnly = false)
    {
        services.PostConfigure<DatabasesOptions>(databases => databases.Declare(name, readOnly));
        return services;
    }

    /// <summary>Registers <see cref="ISqlDatabase{TDatabase}"/> for a database outside EF Core.</summary>
    public static IServiceCollection AddSqlDatabase<TDatabase>(this IServiceCollection services) where TDatabase : ISqlDatabaseDefinition
    {
        services.AddDatabase(TDatabase.Name, TDatabase.ReadOnly);
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
