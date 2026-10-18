using System.Data.Common;
using AiNexus.Platform.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AiNexus.Platform.Data.Sql;

/// <summary>
/// 直接掛在 <typeparamref name="TContext"/> 之下的原生 SQL 執行器。
/// 指令共用 DbContext 的連線並加入其目前的交易，因此同一請求中的原生 SQL 與 EF Core 變更會一起認可或復原。
/// </summary>
public sealed class DbContextSqlDatabase<TContext>(TContext context, AuditContextSource? audit = null) : DapperSqlDatabase<TContext>(audit) where TContext : DbContext
{
    protected override async Task<TResult> RunAsync<TResult>(Func<DbConnection, DbTransaction?, Task<TResult>> command, CancellationToken cancellationToken)
    {
        var database = context.Database;
        // EF Core 以參考計數管理連線：若之前已由 EF Core 或交易開啟，這裡關閉後連線仍會保持開啟。
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
