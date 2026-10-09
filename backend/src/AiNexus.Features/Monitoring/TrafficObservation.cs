using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using AiNexus.Features.Identity;
using Microsoft.Data.SqlClient;

namespace AiNexus.Features.Monitoring;

/// <summary>Suppress observer-generated traffic across middleware, SQL and outgoing HTTP.</summary>
public sealed class MonitoringSuppression : IDisposable
{
    private static readonly AsyncLocal<int> Depth = new();
    public static bool Active => Depth.Value > 0;
    public MonitoringSuppression() => Depth.Value++;
    public void Dispose() => Depth.Value--;
}

public sealed class RuntimeTrafficMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext http, RuntimeTraffic traffic)
    {
        var path = http.Request.Path;
        if (path.StartsWithSegments("/api/v1/presence") || path.StartsWithSegments("/api/v1/admin/monitoring"))
        { using var suppression = new MonitoringSuppression(); await next(http); return; }
        if (!traffic.Enabled || !path.StartsWithSegments("/api/v1")) { await next(http); return; }
        var stream = false;
        traffic.RequestStarted(false);
        http.Response.OnStarting(() => {
            if (http.Response.ContentType?.StartsWith("text/event-stream", StringComparison.OrdinalIgnoreCase) == true)
            { stream = true; traffic.StreamStarted(); }
            return Task.CompletedTask;
        });
        var started = Stopwatch.GetTimestamp();
        var originalRequest = http.Request.Body; var originalResponse = http.Response.Body;
        var request = new TrafficCountingStream(originalRequest, n => traffic.Transferred(n, 0));
        var response = new TrafficCountingStream(originalResponse, written: n => traffic.Transferred(0, n));
        http.Request.Body = request; http.Response.Body = response;
        try { await next(http); }
        finally
        {
            http.Request.Body = originalRequest; http.Response.Body = originalResponse;
            var route = (http.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "/api/v1/{unmatched}";
            var principal = http.User;
            Guid? user = http.RequestServices.GetService<CurrentUser>()?.ResolvedId ??
                (principal.Identity?.IsAuthenticated == true && Guid.TryParse(principal.FindFirst(SessionIdentity.UserId)?.Value, out var id) ? id : null);
            Guid? session = Guid.TryParse(http.Request.Headers["X-Nexus-Session"], out var sid) ? sid : null;
            traffic.RequestFinished(user, session, http.Request.Method, route, http.Response.StatusCode, Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                request.ReadBytes, response.WrittenBytes, stream, http.RequestAborted.IsCancellationRequested, http.Items["Nexus.TraceId"] as string);
        }
    }
}

/// <summary>Counts application body bytes at the shared boundary, including streamed writes. It never retains content.</summary>
public sealed class TrafficCountingStream(Stream inner, Action<long>? received = null, Action<long>? written = null) : Stream
{
    // Stream's own Dispose leaves `inner` open: the request/response feature owns it.
    private long read, sent;
    public long ReadBytes => Interlocked.Read(ref read);
    public long WrittenBytes => Interlocked.Read(ref sent);
    private void CountRead(int count) { Interlocked.Add(ref read, count); if (count > 0) received?.Invoke(count); }
    private void CountWritten(int count) { Interlocked.Add(ref sent, count); if (count > 0) written?.Invoke(count); }
    public override bool CanRead => inner.CanRead; public override bool CanSeek => inner.CanSeek; public override bool CanWrite => inner.CanWrite;
    public override long Length => inner.Length; public override long Position { get => inner.Position; set => inner.Position = value; }
    public override void Flush() => inner.Flush();
    public override Task FlushAsync(CancellationToken ct) => inner.FlushAsync(ct);
    public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
    public override void SetLength(long value) => inner.SetLength(value);
    public override int Read(byte[] buffer, int offset, int count) { var n = inner.Read(buffer, offset, count); CountRead(n); return n; }
    public override int Read(Span<byte> buffer) { var n = inner.Read(buffer); CountRead(n); return n; }
    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) { var n = await inner.ReadAsync(buffer.AsMemory(offset, count), ct); CountRead(n); return n; }
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) { var n = await inner.ReadAsync(buffer, ct); CountRead(n); return n; }
    public override void Write(byte[] buffer, int offset, int count) { inner.Write(buffer, offset, count); CountWritten(count); }
    public override void Write(ReadOnlySpan<byte> buffer) { inner.Write(buffer); CountWritten(buffer.Length); }
    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct) { await inner.WriteAsync(buffer.AsMemory(offset, count), ct); CountWritten(count); }
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default) { await inner.WriteAsync(buffer, ct); CountWritten(buffer.Length); }
}

public sealed class DependencyCatalog
{
    private readonly List<(string Host, int Port, string Id)> http = [];
    private readonly List<(string Server, string Database, string Id)> sql = [];
    public DependencyCatalog(IConfiguration config, RuntimeTraffic traffic)
    {
        AddHttp("https://generativelanguage.googleapis.com", "google", "Google AI");
        AddHttp(config["Inference:Providers:Ollama:Endpoint"], "ollama", "Ollama");
        AddHttp(config["Knowledge:Embedding:Endpoint"], "embedding", "向量模型服務");
        AddHttp(config["Knowledge:Rerank:Endpoint"], "rerank", "重排序服務");
        AddHttp(config["Tools:WebSearch:Endpoint"], "search", "連網搜尋");
        AddHttp(config["Integrations:Connectors:Gitea:BaseUrl"], "gitea", "Gitea 程式庫");
        foreach (var (key, id, name) in new[] { ("Nexus", "sql.nexus", "主資料庫"), ("LegacyGdweb", "sql.gdweb", "公文資料庫"), ("LegacyMeiho", "sql.meiho", "校務資料庫") })
        {
            var connection = config.GetConnectionString(key);
            if (string.IsNullOrWhiteSpace(connection)) continue;
            try
            {
                var parsed = new SqlConnectionStringBuilder(connection);
                sql.Add((parsed.DataSource, parsed.InitialCatalog, id)); traffic.RegisterDependency(id, name, "database");
            }
            catch (ArgumentException) { /* Startup/storage validation owns malformed connection settings. */ }
        }
        traffic.RegisterDependency("http.other", "其他 HTTP 服務", "http");
        traffic.RegisterDependency("sql.other", "其他 SQL 資料庫", "database");
        void AddHttp(string? value, string id, string name)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return;
            // Shared model endpoints represent one transport dependency, without counting each model as a server.
            if (http.Any(x => x.Host.Equals(uri.Host, StringComparison.OrdinalIgnoreCase) && x.Port == uri.Port)) return;
            http.Add((uri.Host, uri.Port, id)); traffic.RegisterDependency(id, name, "http");
        }
    }
    public string Http(Uri? uri) => uri is null ? "http.other" : http.FirstOrDefault(x => x.Host.Equals(uri.Host, StringComparison.OrdinalIgnoreCase) && x.Port == uri.Port).Id ?? "http.other";
    public string Sql(DbConnection? connection) => sql.FirstOrDefault(x => x.Server.Equals(connection?.DataSource, StringComparison.OrdinalIgnoreCase) && x.Database.Equals(connection?.Database, StringComparison.OrdinalIgnoreCase)).Id ?? "sql.other";
}

/// <summary>Shared by every factory client; a call ends at response headers, independently of model streaming.</summary>
public sealed class TrafficHttpHandler(RuntimeTraffic traffic, DependencyCatalog catalog) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (!traffic.Enabled || MonitoringSuppression.Active) return await base.SendAsync(request, ct);
        var id = catalog.Http(request.RequestUri); var start = Stopwatch.GetTimestamp(); traffic.DependencyStarted(id);
        var error = true; var cancelled = false;
        try { var response = await base.SendAsync(request, ct); error = !response.IsSuccessStatusCode; return response; }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { cancelled = true; throw; }
        finally { traffic.DependencyFinished(id, Stopwatch.GetElapsedTime(start).TotalMilliseconds, error, cancelled); }
    }
}

/// <summary>SqlClient's shared diagnostic boundary covers EF, Dapper and direct commands without reading SQL text.</summary>
public sealed class RuntimeSampler(RuntimeTraffic traffic, DependencyCatalog catalog, TimeProvider clock) : BackgroundService, IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>
{
    private readonly ConcurrentDictionary<Guid, (string Id, long Start)> commands = new();
    private readonly List<IDisposable> subscriptions = [];
    public void OnNext(DiagnosticListener source)
    {
        if (source.Name == "SqlClientDiagnosticListener")
            lock (subscriptions) subscriptions.Add(source.Subscribe(this, name => name is "Microsoft.Data.SqlClient.WriteCommandBefore" or "Microsoft.Data.SqlClient.WriteCommandAfter" or "Microsoft.Data.SqlClient.WriteCommandError"));
    }
    public void OnNext(KeyValuePair<string, object?> evt)
    {
        if (!traffic.Enabled || evt.Value is null) return;
        var type = evt.Value.GetType();
        if (type.GetProperty("OperationId")?.GetValue(evt.Value) is not Guid operation) return;
        if (evt.Key.EndsWith("Before", StringComparison.Ordinal))
        {
            if (MonitoringSuppression.Active) return;
            var command = type.GetProperty("Command")?.GetValue(evt.Value) as DbCommand;
            var id = catalog.Sql(command?.Connection);
            lock (commands)
                if (commands.Count < 1024 && commands.TryAdd(operation, (id, Stopwatch.GetTimestamp()))) traffic.DependencyStarted(id);
        }
        else if (commands.TryRemove(operation, out var observed))
            traffic.DependencyFinished(observed.Id, Stopwatch.GetElapsedTime(observed.Start).TotalMilliseconds, evt.Key.EndsWith("Error", StringComparison.Ordinal));
    }
    public void OnCompleted() { }
    public void OnError(Exception error) { }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!traffic.Enabled) return;
        using var all = DiagnosticListener.AllListeners.Subscribe(this);
        using var process = Process.GetCurrentProcess();
        var start = clock.GetUtcNow(); var previous = Stopwatch.GetTimestamp(); var cpu = process.TotalProcessorTime;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(3), clock);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                process.Refresh(); var nextCpu = process.TotalProcessorTime;
                var elapsed = Stopwatch.GetElapsedTime(previous).TotalMilliseconds;
                var percent = Math.Clamp(100 * (nextCpu - cpu).TotalMilliseconds / elapsed / Environment.ProcessorCount, 0, 100);
                traffic.SampleResources(new(Math.Round(percent, 2), process.WorkingSet64, GC.GetTotalMemory(false), process.Threads.Count, (clock.GetUtcNow() - start).TotalSeconds));
                cpu = nextCpu; previous = Stopwatch.GetTimestamp();
                // Missing terminal provider events must not retain unbounded operations/in-flight counts.
                foreach (var pair in commands.Where(x => Stopwatch.GetElapsedTime(x.Value.Start) > TimeSpan.FromMinutes(10)))
                    if (commands.TryRemove(pair.Key, out var stale)) traffic.DependencyFinished(stale.Id, 600000, true);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally { lock (subscriptions) { foreach (var subscription in subscriptions) subscription.Dispose(); subscriptions.Clear(); } }
    }
}
