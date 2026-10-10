using AiNexus.Features.Integrations;
using AiNexus.Platform.Data.Sql;

namespace AiNexus.UnitTests.Integrations;

public sealed class GdwebSourceTests
{
    [Fact]
    public async Task SqlAdapterKeepsSearchValuesInParametersAndRechecksGrantAfterHistory()
    {
        var probe = new QueryProbe(); var adapter = new GdwebSource(probe);
        await adapter.SearchAsync(new("sid-123", "DOMAIN\\actor"), new("' OR 1=1 --%_"), 30, 10, CancellationToken.None);
        Assert.DoesNotContain("OR 1=1", probe.Sql); Assert.Contains("ActorSid = @ActorSid", probe.Sql); Assert.Equal("%' OR 1=1 --~%~_%", probe.Args!.GetType().GetProperty("Pattern")!.GetValue(probe.Args));
        Assert.Equal(30, probe.Args.GetType().GetProperty("Take")!.GetValue(probe.Args));
        probe.RevokeOnRecheck = true; Assert.Equal("source_record_missing", (await adapter.ReadAsync(new("sid-123", "DOMAIN\\actor"), "doc-1", 10, CancellationToken.None)).Error?.Code); Assert.Equal(2, probe.SingleReads);
    }
}

public sealed class QueryProbe : ISqlDatabase<LegacyGdwebDatabase>
{
    public string Sql = ""; public object? Args; public bool RevokeOnRecheck; public int SingleReads;
    public Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? parameters = null, int? commandTimeout = null, CancellationToken cancellationToken = default)
    {
        Sql = sql; Args = parameters; return Task.FromResult<IReadOnlyList<T>>([]);
    }
    public Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? parameters = null, int? commandTimeout = null, CancellationToken cancellationToken = default)
    {
        Sql = sql; Args = parameters; SingleReads++;
        return Task.FromResult((T?)(object?)(RevokeOnRecheck && SingleReads > 1 ? null : new SourceRow { RecordId = "doc-1", Title = "測試", Body = "測試資料", Revision = "v1" }));
    }
    public Task<T> QuerySingleAsync<T>(string sql, object? parameters = null, int? commandTimeout = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<int> ExecuteAsync(string sql, object? parameters = null, int? commandTimeout = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
