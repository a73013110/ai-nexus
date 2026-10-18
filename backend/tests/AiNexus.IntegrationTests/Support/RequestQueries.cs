using AiNexus.Data.App;
using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace AiNexus.IntegrationTests.Support;

/// <summary>處理請求路徑期間發出的 SQL 指令；背景工作沒有請求，因此不計入。</summary>
public sealed class RequestQueries(IHttpContextAccessor http) : DbCommandInterceptor
{
    private readonly ConcurrentQueue<(string Path, string Sql)> commands = new();

    public static void Register(IServiceCollection services)
    {
        services.AddSingleton<RequestQueries>();
        services.ConfigureDbContext<AppDbContext>((provider, options) => options.AddInterceptors(provider.GetRequiredService<RequestQueries>()));
    }

    public void Clear() => commands.Clear();
    /// <summary>對應 <paramref name="path"/> 的指令，且其 SQL 包含 <paramref name="texts"/> 中的每一段文字。</summary>
    public int Count(string path, params string[] texts) => commands.Count(x => x.Path == path && texts.All(text => x.Sql.Contains(text, StringComparison.Ordinal)));

    private void Record(DbCommand command)
    {
        if (http.HttpContext?.Request.Path.Value is { } path) commands.Enqueue((path, command.CommandText));
    }
    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData data, InterceptionResult<DbDataReader> result) { Record(command); return result; }
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData data, InterceptionResult<DbDataReader> result, CancellationToken ct = default) { Record(command); return ValueTask.FromResult(result); }
    public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData data, InterceptionResult<object> result) { Record(command); return result; }
    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData data, InterceptionResult<object> result, CancellationToken ct = default) { Record(command); return ValueTask.FromResult(result); }
    public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData data, InterceptionResult<int> result) { Record(command); return result; }
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData data, InterceptionResult<int> result, CancellationToken ct = default) { Record(command); return ValueTask.FromResult(result); }
}
