using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json.Nodes;
using AiNexus.Data.App;
using AiNexus.Data.App.Audit;
using AiNexus.Features.Audit;
using AiNexus.Platform.Data.Sql;
using AiNexus.Platform.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AiNexus.IntegrationTests.Support;

/// <summary>
/// procedure 的稽核檢查（ADR 0014「明細與程式一致」）。<see cref="Register"/> 包住 <c>ISqlDatabase&lt;AppDbContext&gt;</c>，
/// 記下每次 procedure 呼叫當下的 TraceId 與它寫出的稽核列範圍；<see cref="AssertWrittenAsync"/> 檢查成功呼叫寫出的每一筆。
/// 每支 procedure 的整合測試都要用它：寫入的檢查稽核，讀取的確認真的經過 procedure 且沒有寫稽核。
/// </summary>
/// <remarks>At 要與呼叫端的 TimeProvider 一致，所以測試要以固定的 <c>FakeTimeProvider</c> 建 host。</remarks>
public sealed class ProcedureAudits
{
    private readonly ConcurrentQueue<Call> calls = new();

    /// <summary>傳給 <c>AppFactory</c> 的 <c>services</c>。</summary>
    public void Register(IServiceCollection services) => services.Replace(ServiceDescriptor.Scoped<ISqlDatabase<AppDbContext>>(
        scope => new Recorder(ActivatorUtilities.CreateInstance<DbContextSqlDatabase<AppDbContext>>(scope), calls)));

    /// <summary>
    /// <paramref name="procedure"/> 至少成功呼叫過一次，且每次都寫了稽核；每一筆的有效身分、TraceId、時間與呼叫端一致，
    /// 明細經 <see cref="AuditRedactor.Sanitize"/> 後內容不變，動作歸在 <paramref name="category"/>。
    /// </summary>
    public async Task<IReadOnlyList<AuditEvent>> AssertWrittenAsync(AppFactory factory, string procedure, Guid owner, DateTimeOffset at, string category)
    {
        var succeeded = Calls(procedure).Where(x => x.Succeeded).ToArray();
        Assert.NotEmpty(succeeded);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var written = new List<AuditEvent>();
        foreach (var call in succeeded)
        {
            var rows = await db.AuditEvents.AsNoTracking().Where(x => x.Id > call.Before && x.Id <= call.After).OrderBy(x => x.Id).ToListAsync();
            Assert.NotEmpty(rows);
            foreach (var row in rows)
            {
                Assert.Equal(owner, row.OwnerId);
                Assert.Equal(call.TraceId, row.TraceId);
                Assert.NotNull(row.TraceId);
                Assert.Equal(at, row.At);
                Assert.Equal(category, AuditCategories.For(row.Action));
                if (row.DetailsJson is not null)
                    Assert.True(JsonNode.DeepEquals(JsonNode.Parse(row.DetailsJson), JsonNode.Parse(AuditRedactor.Sanitize(row.DetailsJson)!)),
                        $"{procedure} 的稽核明細含有 AuditRedactor 會移除或改寫的內容：{row.DetailsJson}");
            }
            written.AddRange(rows);
        }
        return written;
    }

    /// <summary>讀取用的 procedure：確實被呼叫過，且沒有寫任何稽核。</summary>
    public void AssertReadOnly(string procedure)
    {
        var made = Calls(procedure);
        Assert.NotEmpty(made);
        Assert.All(made, call => { Assert.True(call.Succeeded); Assert.Equal(call.Before, call.After); });
    }

    private Call[] Calls(string procedure) => [.. calls.Where(x => x.Procedure == procedure)];

    private sealed record Call(string Procedure, string? TraceId, long Before, long After, bool Succeeded);

    private sealed class Recorder(ISqlDatabase<AppDbContext> inner, ConcurrentQueue<Call> calls) : ISqlDatabase<AppDbContext>
    {
        public Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? parameters = null, int? commandTimeout = null, CancellationToken cancellationToken = default)
            => inner.QueryAsync<T>(sql, parameters, commandTimeout, cancellationToken);
        public Task<T> QuerySingleAsync<T>(string sql, object? parameters = null, int? commandTimeout = null, CancellationToken cancellationToken = default)
            => inner.QuerySingleAsync<T>(sql, parameters, commandTimeout, cancellationToken);
        public Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? parameters = null, int? commandTimeout = null, CancellationToken cancellationToken = default)
            => inner.QuerySingleOrDefaultAsync<T>(sql, parameters, commandTimeout, cancellationToken);
        public Task<int> ExecuteAsync(string sql, object? parameters = null, int? commandTimeout = null, CancellationToken cancellationToken = default)
            => inner.ExecuteAsync(sql, parameters, commandTimeout, cancellationToken);
        public Task<IReadOnlyList<T>> QueryProcedureAsync<T>(string procedure, object? parameters = null, int? commandTimeout = null, CancellationToken cancellationToken = default)
            => RecordAsync(procedure, () => inner.QueryProcedureAsync<T>(procedure, parameters, commandTimeout, cancellationToken), cancellationToken);
        public Task<TResult> QueryProcedureAsync<TResult>(string procedure, object? parameters, Func<SqlResults, Task<TResult>> read, int? commandTimeout = null, CancellationToken cancellationToken = default)
            => RecordAsync(procedure, () => inner.QueryProcedureAsync(procedure, parameters, read, commandTimeout, cancellationToken), cancellationToken);
        public Task<IReadOnlyList<T>> ExecuteProcedureAsync<T>(string procedure, object? parameters = null, Guid? auditOwner = null, int? commandTimeout = null, CancellationToken cancellationToken = default)
            => RecordAsync(procedure, () => inner.ExecuteProcedureAsync<T>(procedure, parameters, auditOwner, commandTimeout, cancellationToken), cancellationToken);

        // 前後各讀一次最大 Id：在同一個連線與交易內，看得到 procedure 自己尚未提交的稽核列。
        private async Task<T> RecordAsync<T>(string procedure, Func<Task<T>> call, CancellationToken ct)
        {
            const string last = "SELECT ISNULL(MAX(Id), 0) FROM audit.AuditEvents";
            var trace = Activity.Current?.TraceId.ToHexString();
            var before = await inner.QuerySingleAsync<long>(last, cancellationToken: ct);
            try
            {
                var result = await call();
                calls.Enqueue(new(procedure, trace, before, await inner.QuerySingleAsync<long>(last, cancellationToken: ct), true));
                return result;
            }
            catch
            {
                calls.Enqueue(new(procedure, trace, before, before, false));
                throw;
            }
        }
    }
}
