using AiNexus.Features.Admin.Monitoring;
using AiNexus.Features.Admin.Monitoring.Traffic;
using AiNexus.Platform.Data;
using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace AiNexus.UnitTests.Monitoring;

public sealed class RuntimeSamplerTests
{
    private static RuntimeTraffic Traffic(FakeTimeProvider clock, int capacity = 2000) => new(Options.Create(new MonitoringOptions { MaxSessions = capacity }), clock);

    // 用真的 SqlClient 診斷事件：連線沒開時執行命令，SqlClient 仍會送出 WriteCommandBefore 與 WriteCommandError，不需要資料庫。
    // 事件先錄下再交給取樣器，不依賴背景服務何時訂閱；只留這個命令的事件，平行執行的其他測試不影響結果。
    [Fact]
    public async Task SqlObserverReadsOnlyConnectionClassificationAndTracksFailures()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 8, 0, 0, 0, TimeSpan.Zero)); var traffic = Traffic(clock);
        var databases = new DatabasesOptions { ["App"] = new() { Server = "fixture", Name = "fixture-db", IntegratedSecurity = true } };
        var catalog = new DependencyCatalog(new ConfigurationBuilder().Build(), Options.Create(databases), traffic);
        using var observer = new RuntimeSampler(traffic, catalog, clock);
        await using var connection = new SqlConnection(databases.ConnectionString("App"));
        await using var command = new SqlCommand("SELECT 'private-password-prompt'", connection);
        var recorder = new SqlEvents(command);
        using (recorder) await Assert.ThrowsAsync<InvalidOperationException>(() => command.ExecuteNonQueryAsync(ct));
        Assert.Equal(2, recorder.Events.Count);

        observer.OnNext(recorder.Events[0]);
        Assert.Equal(1, traffic.Snapshot(1).Dependencies.Single(x => x.Id == "sql.app").InFlight);
        observer.OnNext(recorder.Events[1]);
        clock.Advance(TimeSpan.FromSeconds(10)); var dep = traffic.Snapshot(1).Dependencies.Single(x => x.Id == "sql.app");
        Assert.Equal("failing", dep.Status); Assert.Equal(1, dep.Metrics.Errors); Assert.Equal(0, dep.InFlight);
        Assert.DoesNotContain("private", System.Text.Json.JsonSerializer.Serialize(traffic.Snapshot(1)));
    }

    /// <summary>錄下指定命令的 SqlClient 命令事件（依發生順序）。</summary>
    private sealed class SqlEvents : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>, IDisposable
    {
        private readonly SqlCommand command;
        private readonly List<IDisposable> subscriptions = [];

        public SqlEvents(SqlCommand command)
        {
            this.command = command;
            subscriptions.Add(DiagnosticListener.AllListeners.Subscribe(this));
        }

        public List<KeyValuePair<string, object?>> Events { get; } = [];

        public void OnNext(DiagnosticListener value)
        {
            if (value.Name == "SqlClientDiagnosticListener") lock (subscriptions) subscriptions.Add(value.Subscribe(this));
        }

        public void OnNext(KeyValuePair<string, object?> value)
        {
            var owner = value.Value switch { SqlClientCommandBefore x => x.Command, SqlClientCommandError x => x.Command, _ => null };
            if (ReferenceEquals(owner, command)) lock (Events) Events.Add(value);
        }

        public void OnCompleted() { }
        public void OnError(Exception error) { }

        public void Dispose()
        {
            lock (subscriptions) { foreach (var subscription in subscriptions) subscription.Dispose(); subscriptions.Clear(); }
        }
    }
}
