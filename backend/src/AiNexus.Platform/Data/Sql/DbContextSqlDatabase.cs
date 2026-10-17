using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AiNexus.Platform.Data.Sql;

/// <summary>
/// 直接掛載於 <typeparamref name="TContext"/> 之下的原生 SQL 執行器：
/// 指令共用 DbContext 的資料庫連線並加入其當前交易，使同一請求中的原生 SQL 與 EF Core 變更能夠一同認可或復原。
/// </summary>
public sealed class DbContextSqlDatabase<TContext>(TContext context) : DapperSqlDatabase<TContext> where TContext : DbContext
{
    protected override async Task<TResult> RunAsync<TResult>(Func<DbConnection, DbTransaction?, Task<TResult>> command, CancellationToken cancellationToken)
    {
        var database = context.Database;
        // 連線由 EF Core 進行參考計數：若先前已由 EF Core 或交易開啟，此處呼叫後連線在完成後仍會保持開啟
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
