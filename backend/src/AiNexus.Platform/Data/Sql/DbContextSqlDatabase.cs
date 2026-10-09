using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AiNexus.Platform.Data.Sql;

/// <summary>
/// The database behind <typeparamref name="TContext"/>: commands use the context's connection and join its current
/// transaction, so SQL and EF Core writes in one request commit or roll back together.
/// </summary>
public sealed class DbContextSqlDatabase<TContext>(TContext context) : DapperSqlDatabase<TContext> where TContext : DbContext
{
    protected override async Task<TResult> RunAsync<TResult>(Func<DbConnection, DbTransaction?, Task<TResult>> command, CancellationToken cancellationToken)
    {
        var database = context.Database;
        // Reference-counted by EF Core: a connection that EF (or a transaction) already opened stays open afterwards.
        await database.OpenConnectionAsync(cancellationToken);
        try
        {
            return await command(database.GetDbConnection(), database.CurrentTransaction?.GetDbTransaction());
        }
        finally
        {
            await database.CloseConnectionAsync();
        }
    }
}
