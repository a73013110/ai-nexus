using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AiNexus.BuildingBlocks;
using AiNexus.BuildingBlocks.Diagnostics;
using AiNexus.Modules.AccessControl;
using AiNexus.Modules.Inference;
using AiNexus.Modules.Operations;
using AiNexus.Modules.Knowledge;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Xunit;
using static AiNexus.Tests.ChatApiTests;

namespace AiNexus.Tests;

[CollectionDefinition("Diagnostic integration", DisableParallelization = true)]
public sealed class DiagnosticIntegrationCollection;
[Collection("Diagnostic integration")]
public sealed class DiagnosticTests
{
    private const string Secret = "private-provider-password-123";
    [Fact]
    public void ReviewedPublicHintsStayIdenticalAcrossBackendAndFrontend()
    {
        var root = Path.GetFullPath("../../../../../..", AppContext.BaseDirectory);
        var backend = File.ReadAllText(Path.Combine(root, "backend/src/AiNexus.Api/BuildingBlocks/Diagnostics/PublicErrorCatalog.cs"));
        var frontend = File.ReadAllText(Path.Combine(root, "frontend/src/app/core/api/public-error-catalog.ts"));
        var expected = System.Text.RegularExpressions.Regex.Matches(backend, "\\[\"(?<key>[a-z0-9_]+)\"\\]\\s*=\\s*\"(?<hint>[^\"\\r\\n]*)\"")
            .ToDictionary(m => m.Groups["key"].Value, m => m.Groups["hint"].Value);
        var actual = System.Text.RegularExpressions.Regex.Matches(frontend, @"^\s*(?<key>[a-z0-9_]+):\s*'(?<hint>[^']*)'", System.Text.RegularExpressions.RegexOptions.Multiline)
            .ToDictionary(m => m.Groups["key"].Value, m => m.Groups["hint"].Value);
        Assert.NotEmpty(expected); Assert.Equal(expected.Count, actual.Count);
        foreach (var hint in expected) { Assert.True(actual.TryGetValue(hint.Key, out var value), "Missing fixed public hint: " + hint.Key); Assert.Equal(hint.Value, value); }
    }
    [Fact]
    public void BoundaryOmitsArbitraryObjectsAndContentAndScrubsInjection()
    {
        var values = DiagnosticRedactor.Properties(new Dictionary<string, object?> {
            ["Password"] = Secret, ["Authorization"] = "Bearer " + Secret, ["Prompt"] = Secret, ["Query"] = Secret,
            ["Count"] = new DangerousObject(), ["Reason"] = "password=" + Secret + " https://internal.test?q=" + Secret + " C:\\private\\secret.txt\r\nforged record",
            ["SqlNumber"] = 30053
        });
        var json = DiagnosticRedactor.Json(values); Assert.DoesNotContain(Secret, json); Assert.DoesNotContain("internal.test", json);
        Assert.DoesNotContain("private\\", json); Assert.DoesNotContain("\r", json); Assert.Contains("30053", json); Assert.Contains("OBJECT OMITTED", json);
        var detail = DiagnosticRedactor.Exception(new InvalidOperationException(Secret, new HttpRequestException("Bearer " + Secret)));
        Assert.DoesNotContain(Secret, detail); Assert.Contains("InvalidOperationException", detail); Assert.Contains("HttpRequestException", detail);
        Assert.True(DiagnosticRedactor.Text(new string('a', 100000)).Length <= 512);
    }
    private sealed class DangerousObject { public override string ToString() => throw new Exception("Arbitrary object must never be rendered"); }
    [Fact]
    public void CodeBearingRejectionsBypassMinimumAndSamplingAndRetainTrustedCorrelation()
    {
        using var health = new DiagnosticHealth(); var options = Options.Create(new DiagnosticOptions { MinimumLevel = LogLevel.Critical, LowLevelSampleEvery = 1000 });
        var buffer = new DiagnosticBuffer(options, health);
        using var provider = new DiagnosticLoggerProvider(buffer, options, new EnvironmentFixture());
        using var factory = LoggerFactory.Create(x => x.SetMinimumLevel(LogLevel.Trace).AddProvider(provider));
        using var trace = DiagnosticTrace.Start("validation.fixture");
        var issues = new Issues(factory.CreateLogger<Issues>(), factory);
        var problem = issues.Problem(new ApiException(400, "invalid_request", Secret));
        var row = Assert.Single(buffer.Drain()); Assert.Equal(problem.IssueCode, row.IssueCode); Assert.Equal(LogLevel.Information, row.Level);
        Assert.Equal(trace.TraceId.ToHexString(), row.TraceId); Assert.Equal(0, health.Sampled); Assert.DoesNotContain(Secret, problem.Title);
    }

    [Fact]
    public async Task AuditBoundaryFailsClosedMasksLegacyAndShowsSystemConfiguration()
    {
        Assert.Throws<ApiException>(() => AuditRedactor.Sanitize("{"));
        var safe = AuditRedactor.Sanitize(JsonSerializer.Serialize(new { password = Secret, after = new { enabled = true, token = Secret }, reason = Secret }));
        Assert.DoesNotContain(Secret, safe); Assert.Contains("enabled", safe); Assert.Contains("OMITTED", safe);
        await using var factory = new NexusFactory(administrators: ["alice"]); using var client = await factory.SignedInAsync();
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        var actor = (await client.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        var legacy = new AuditEvent { OwnerId = actor, Action = "fixture.legacy", Result = "saved" }; db.Add(legacy); await db.SaveChangesAsync();
        var raw = JsonSerializer.Serialize(new { password = Secret, before = new { token = Secret, enabled = true }, reason = Secret });
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE AuditEvents SET DetailsJson={raw} WHERE Id={legacy.Id}");
        var response = await client.GetStringAsync("/api/v1/admin/audit?action=fixture.legacy"); Assert.DoesNotContain(Secret, response);
        await factory.Services.GetRequiredService<IDiagnosticStore>().CleanupAsync(CancellationToken.None);
        var system = await client.GetStringAsync("/api/v1/admin/audit?action=system.diagnostics.configuration"); Assert.Contains("system", system); Assert.Contains("fingerprint", system);
        Assert.DoesNotContain("nexus-test-", system); Assert.DoesNotContain("localhost:4318", system);
    }

    [Fact]
    public async Task SqlCapacityAllowsDuplicateReplayAndRetentionKeepsDistinctAuditPolicy()
    {
        await using var factory = new NexusFactory(services: services => services.PostConfigure<DiagnosticOptions>(x => { x.MaxSqlRows = 1000; x.CleanupBatchSize = 10; }));
        using var client = factory.CreateClient(); (await client.GetAsync("/health/live")).EnsureSuccessStatusCode();
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        var old = Enumerable.Range(0, 1000).Select(_ => new DiagnosticEvent { At = DateTimeOffset.UtcNow.AddDays(-31), Level = LogLevel.Information }).ToArray();
        db.AddRange(old); var retained = new AuditEvent { Action = "fixture.retained", At = DateTimeOffset.UtcNow.AddDays(-31) }; var expired = new AuditEvent { Action = "fixture.expired", At = DateTimeOffset.UtcNow.AddDays(-366) };
        db.AddRange(retained, expired); await db.SaveChangesAsync(); var store = factory.Services.GetRequiredService<IDiagnosticStore>();
        await store.WriteAsync([old[0]], CancellationToken.None); // Duplicate replay is safe even above the soft capacity limit.
        await Assert.ThrowsAsync<DiagnosticCapacityException>(() => store.WriteAsync([new DiagnosticEvent()], CancellationToken.None));
        await store.CleanupAsync(CancellationToken.None);
        Assert.False(await db.Set<DiagnosticEvent>().AnyAsync(x => x.LogId == old[0].LogId)); Assert.True(await db.AuditEvents.AnyAsync(x => x.Id == retained.Id));
        Assert.False(await db.AuditEvents.AnyAsync(x => x.Id == expired.Id)); await store.WriteAsync([new DiagnosticEvent()], CancellationToken.None);
    }

    [Fact]
    public async Task UnavailableOptionalExporterDoesNotLoseDurableEvents()
    {
        using var health = new DiagnosticHealth();
        using var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0); listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
        var options = Options.Create(new DiagnosticOptions { OtlpEnabled = true, OtlpEndpoint = "http://127.0.0.1:" + port });
        using var exporter = new DiagnosticExporter(options, health);
        var directory = Path.Combine(Path.GetTempPath(), "nexus-otlp-" + Guid.NewGuid().ToString("N"));
        try {
            var fileOptions = Options.Create(new DiagnosticOptions { Directory = directory }); using var journal = new DiagnosticJournal(fileOptions, health, new EnvironmentFixture());
            using var trace = DiagnosticTrace.Start("export.fixture"); var row = new DiagnosticEvent { IssueCode = Issues.NewCode(), TraceId = trace.TraceId.ToHexString(), SpanId = trace.SpanId.ToHexString(), Level = LogLevel.Error, MessageTemplate = "Safe exporter fixture." };
            await journal.AppendAsync([row], CancellationToken.None); exporter.Export([row]);
            for (var attempt = 0; attempt < 100 && health.ExportFailures == 0; attempt++) await Task.Delay(50);
            Assert.True(health.ExportFailures > 0); var store = new MemoryStore(); await journal.ReplayAsync(store, CancellationToken.None);
            Assert.Single(store.Events); Assert.Equal(0, health.Lost); Assert.Equal(row.TraceId, store.Events[row.LogId].TraceId);
        } finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task OtlpWirePreservesOriginalTraceSpanTimestampAndMaskedMetadata()
    {
        var builder = WebApplication.CreateSlimBuilder(); builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var collector = builder.Build(); var received = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        collector.MapPost("/v1/logs", async (HttpContext http) => { using var bytes = new MemoryStream(); await http.Request.Body.CopyToAsync(bytes); received.TrySetResult(bytes.ToArray()); http.Response.StatusCode = 200; http.Response.ContentType = "application/x-protobuf"; });
        await collector.StartAsync();
        var endpoint = collector.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var health = new DiagnosticHealth(); using var exporter = new DiagnosticExporter(Options.Create(new DiagnosticOptions { OtlpEnabled = true, OtlpEndpoint = endpoint }), health);
        var at = DateTimeOffset.Parse("2026-01-02T03:04:05Z", System.Globalization.CultureInfo.InvariantCulture);
        var row = new DiagnosticEvent { IssueCode = Issues.NewCode(), TraceId = "1234567890abcdef1234567890abcdef", SpanId = "1234567890abcdef", At = at, Level = LogLevel.Error, MessageTemplate = "Controlled OTLP fixture.",
            PropertiesJson = JsonSerializer.Serialize(new { Password = Secret, SqlNumber = 30053 }), ExceptionDetail = "password=" + Secret };
        exporter.Export([row]); var body = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Contains(row.IssueCode!, Encoding.UTF8.GetString(body)); Assert.DoesNotContain(Secret, Encoding.UTF8.GetString(body));
        Assert.True(body.AsSpan().IndexOf(Convert.FromHexString(row.TraceId)) >= 0); Assert.True(body.AsSpan().IndexOf(Convert.FromHexString(row.SpanId)) >= 0);
        Assert.True(body.AsSpan().IndexOf(BitConverter.GetBytes((ulong)at.ToUnixTimeMilliseconds() * 1_000_000)) >= 0, "OTLP time_unix_nano must represent the original event, not export time.");
        await collector.StopAsync();
    }

    [Fact]
    public async Task StartedSseFailureUsesSafeEventAndOneQueryableIssue()
    {
        using var health = new DiagnosticHealth(); var options = Options.Create(new DiagnosticOptions()); var buffer = new DiagnosticBuffer(options, health);
        using var loggerProvider = new DiagnosticLoggerProvider(buffer, options, new EnvironmentFixture());
        using var services = new ServiceCollection().AddLogging(x => x.AddProvider(loggerProvider)).AddSingleton<Issues>().BuildServiceProvider();
        using var body = new MemoryStream(); var http = new DefaultHttpContext { RequestServices = services };
        http.Features.Set<IHttpResponseFeature>(new StartedSseResponse()); http.Response.Body = body; http.Response.ContentType = "text/event-stream";
        var failure = new HttpRequestException("Bearer " + Secret + " https://private.test/provider");
        var boundary = new DiagnosticRequestMiddleware(_ => throw failure);
        await boundary.InvokeAsync(http, services.GetRequiredService<Issues>(), services.GetRequiredService<ILogger<DiagnosticRequestMiddleware>>());
        var response = Encoding.UTF8.GetString(body.ToArray()); Assert.Contains("event: error", response); Assert.DoesNotContain(Secret, response); Assert.DoesNotContain("private.test", response);
        var issue = services.GetRequiredService<Issues>().Report(failure, "service_unavailable");
        Assert.Contains(issue, response); Assert.Single(buffer.Drain(), x => x.IssueCode == issue);
    }
    private sealed class StartedSseResponse : HttpResponseFeature { public override bool HasStarted => true; }

    [Fact]
    public async Task DisposedJournalCannotReopenItsCapacityOrOwnerHandles()
    {
        var directory = Path.Combine(Path.GetTempPath(), "nexus-disposed-" + Guid.NewGuid().ToString("N")); using var health = new DiagnosticHealth();
        var journal = new DiagnosticJournal(Options.Create(new DiagnosticOptions { Directory = directory }), health, new EnvironmentFixture());
        try { await journal.AppendAsync([new DiagnosticEvent()], CancellationToken.None); journal.Dispose();
            using (var lease = new FileStream(Path.Combine(directory, "capacity.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None)) Assert.True(lease.CanWrite);
            await Assert.ThrowsAsync<ObjectDisposedException>(() => journal.AppendAsync([new DiagnosticEvent()], CancellationToken.None));
        } finally { journal.Dispose(); Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task RotationBoundsUnicodeRecordsAndSharedCapacityAcrossLiveInstances()
    {
        var directory = Path.Combine(Path.GetTempPath(), "nexus-unicode-" + Guid.NewGuid().ToString("N")); using var health = new DiagnosticHealth(); using var otherHealth = new DiagnosticHealth();
        var options = Options.Create(new DiagnosticOptions { Directory = directory, FileSizeBytes = 65536, MaxDiskBytes = 180000 });
        try {
            using var first = new DiagnosticJournal(options, health, new EnvironmentFixture()); using var second = new DiagnosticJournal(options, otherHealth, new EnvironmentFixture());
            var detail = string.Join('\n', Enumerable.Repeat(new string('測', 200), 60));
            var rows = Enumerable.Range(0, 3).Select(_ => new DiagnosticEvent { Level = LogLevel.Error, MessageTemplate = new string('中', 2048), ExceptionDetail = detail, IssueCode = Issues.NewCode() }).ToArray();
            await first.AppendAsync(rows, CancellationToken.None); await second.AppendAsync([new() { Level = LogLevel.Error }], CancellationToken.None);
            Assert.All(Directory.EnumerateFiles(directory, "*.jsonl", SearchOption.AllDirectories), f => Assert.True(new FileInfo(f).Length <= 65536));
            await Assert.ThrowsAnyAsync<IOException>(() => second.AppendAsync(rows.Select(_ => new DiagnosticEvent { Level = LogLevel.Error, ExceptionDetail = detail }).ToArray(), CancellationToken.None));
            var store = new MemoryStore(); await first.ReplayAsync(store, CancellationToken.None); Assert.Equal(3, store.Events.Count); // Other live owner is not stolen.
            await second.ReplayAsync(store, CancellationToken.None); Assert.Equal(4, store.Events.Count); Assert.Equal(0, otherHealth.Corrupt);
        } finally { Directory.Delete(directory, true); }
    }
    [Fact]
    public void RecoveryBoundsAllMetadataAndFiltersNestedProperties()
    {
        var item = new DiagnosticEvent { Category = "password=" + Secret, Instance = "https://internal.test/secret", TraceId = "spoof", IssueCode = "user-code",
            PropertiesJson = JsonSerializer.Serialize(new { Token = Secret, Prompt = Secret, Reason = "Bearer " + Secret, Count = new { secret = Secret }, SqlNumber = 30053 }) };
        DiagnosticRedactor.Normalize(item); Assert.DoesNotContain(Secret, JsonSerializer.Serialize(item)); Assert.DoesNotContain("internal.test", item.Instance);
        Assert.Null(item.TraceId); Assert.Null(item.IssueCode); Assert.Contains("30053", item.PropertiesJson);
    }

    [Fact]
    public void QueueLimitsAreVisibleAndErrorCriticalAreNotSampled()
    {
        using var health = new DiagnosticHealth(); var options = Options.Create(new DiagnosticOptions { QueueCapacity = 1, ImportantQueueCapacity = 2, LowLevelSampleEvery = 10 });
        var buffer = new DiagnosticBuffer(options, health);
        for (var i = 0; i < 20; i++) buffer.Enqueue(new() { Level = LogLevel.Debug });
        buffer.Enqueue(new() { Level = LogLevel.Error }); buffer.Enqueue(new() { Level = LogLevel.Critical }); buffer.Enqueue(new() { Level = LogLevel.Error });
        var rows = buffer.Drain(); Assert.Contains(rows, x => x.Level == LogLevel.Critical); Assert.Contains(rows, x => x.Level == LogLevel.Error);
        Assert.Equal(18, health.Sampled); Assert.Equal(2, health.Lost); Assert.Equal(0, health.QueueDepth); Assert.Equal("degraded", health.Snapshot().Status);
    }

    [Fact]
    public async Task JournalSurvivesStoreOutageAndRestartAndDoesNotDuplicateAfterLostCheckpoint()
    {
        var directory = Path.Combine(Path.GetTempPath(), "nexus-diagnostics-" + Guid.NewGuid().ToString("N"));
        using var health = new DiagnosticHealth(); var options = Options.Create(new DiagnosticOptions { Directory = directory, BatchSize = 2 });
        var store = new MemoryStore { Offline = true }; var records = Enumerable.Range(0, 5).Select(_ => new DiagnosticEvent { Level = LogLevel.Error, IssueCode = Issues.NewCode() }).ToArray();
        try
        {
            using (var journal = new DiagnosticJournal(options, health, new EnvironmentFixture())) {
                await journal.AppendAsync(records, CancellationToken.None); await Assert.ThrowsAsync<IOException>(() => journal.ReplayAsync(store, CancellationToken.None));
                Assert.Equal(5, health.Written); Assert.Empty(store.Events);
            }
            store.Offline = false;
            using (var recovered = new DiagnosticJournal(options, health, new EnvironmentFixture())) { await recovered.ReplayAsync(store, CancellationToken.None); }
            Assert.Equal(5, store.Events.Count); Assert.All(records, x => Assert.Contains(x.LogId, store.Events.Keys));
            foreach (var file in Directory.EnumerateFiles(directory, "*.cursor", SearchOption.AllDirectories)) File.Delete(file);
            using (var replay = new DiagnosticJournal(options, health, new EnvironmentFixture())) { await replay.ReplayAsync(store, CancellationToken.None); }
            Assert.Equal(5, store.Events.Count); Assert.Equal(0, health.PendingBytes);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task CorruptAndTruncatedJournalRecordsAreCountedAndValidDataContinues()
    {
        var directory = Path.Combine(Path.GetTempPath(), "nexus-corrupt-" + Guid.NewGuid().ToString("N"));
        using var health = new DiagnosticHealth(); var options = Options.Create(new DiagnosticOptions { Directory = directory }); var store = new MemoryStore();
        try {
            using (var journal = new DiagnosticJournal(options, health, new EnvironmentFixture())) { await journal.AppendAsync([new() { Level = LogLevel.Error }], CancellationToken.None); }
            var file = Directory.EnumerateFiles(directory, "*.jsonl", SearchOption.AllDirectories).Single(); await File.AppendAllTextAsync(file, "{corrupt}\n{unfinished");
            using (var replay = new DiagnosticJournal(options, health, new EnvironmentFixture())) { await replay.ReplayAsync(store, CancellationToken.None); }
            Assert.Single(store.Events); Assert.Equal(2, health.Corrupt); Assert.Equal(2, health.Lost); Assert.Equal(0, health.PendingBytes);
        } finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task RetentionPreservesUnacknowledgedFilesButRemovesOldCommittedSegments()
    {
        var directory = Path.Combine(Path.GetTempPath(), "nexus-retention-" + Guid.NewGuid().ToString("N")); using var health = new DiagnosticHealth();
        var options = Options.Create(new DiagnosticOptions { Directory = directory, FileRetentionDays = 1 }); var store = new MemoryStore();
        try {
            using (var journal = new DiagnosticJournal(options, health, new EnvironmentFixture())) { await journal.AppendAsync([new() { Level = LogLevel.Error }], CancellationToken.None); }
            var file = Directory.EnumerateFiles(directory, "*.jsonl", SearchOption.AllDirectories).Single(); File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddDays(-3));
            using var replay = new DiagnosticJournal(options, health, new EnvironmentFixture()); replay.Cleanup(); Assert.True(File.Exists(file));
            await replay.ReplayAsync(store, CancellationToken.None); replay.Cleanup(); Assert.False(File.Exists(file));
        } finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task InvalidPathAndCapacityCannotSilentlySucceed()
    {
        using var health = new DiagnosticHealth(); var environment = new EnvironmentFixture();
        Assert.Throws<InvalidOperationException>(() => DiagnosticJournal.Resolve("relative/logs", environment));
        Assert.Throws<InvalidOperationException>(() => DiagnosticJournal.Resolve(environment.ContentRootPath, environment));
        var path = Path.Combine(Path.GetTempPath(), "nexus-no-directory-" + Guid.NewGuid().ToString("N")); await File.WriteAllTextAsync(path, "locked path");
        try { using var journal = new DiagnosticJournal(Options.Create(new DiagnosticOptions { Directory = path }), health, environment); await Assert.ThrowsAnyAsync<IOException>(() => journal.AppendAsync([new() { Level = LogLevel.Error }], CancellationToken.None)); }
        finally { File.Delete(path); }
        var directory = Path.Combine(Path.GetTempPath(), "nexus-capacity-" + Guid.NewGuid().ToString("N"));
        try { using var journal = new DiagnosticJournal(Options.Create(new DiagnosticOptions { Directory = directory, MaxDiskBytes = 1 }), health, environment); await Assert.ThrowsAnyAsync<IOException>(() => journal.AppendAsync([new() { Level = LogLevel.Error }], CancellationToken.None)); }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task ClientReportIsUntrustedBoundedDeduplicatedAndPrivate()
    {
        await using var factory = new NexusFactory(administrators: ["alice"]); using var alice = await factory.SignedInAsync(); using var bob = await factory.SignedInAsync("bob");
        var body = new ClientIssueRequest("exception", new string('A', 64));
        var first = await alice.PostAsJsonAsync("/api/v1/client-issues", body); first.EnsureSuccessStatusCode(); var code = (await first.Content.ReadFromJsonAsync<ClientIssueResponse>())!.IssueCode;
        var again = await alice.PostAsJsonAsync("/api/v1/client-issues", body); Assert.Equal(code, (await again.Content.ReadFromJsonAsync<ClientIssueResponse>())!.IssueCode);
        var invalid = await alice.PostAsJsonAsync("/api/v1/client-issues", new { kind = Secret, fingerprint = Secret }); Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode); Assert.DoesNotContain(Secret, await invalid.Content.ReadAsStringAsync());
        var entry = await WaitForIssue(factory, code); Assert.True(entry.UntrustedClient); Assert.NotNull(entry.RequestId); Assert.NotNull(entry.TraceId);
        Assert.Equal(1, await CountIssue(factory, code));
        Assert.Equal(HttpStatusCode.Forbidden, (await bob.GetAsync("/api/v1/admin/logs?issueCode=" + code)).StatusCode);
        using var anonymous = factory.CreateClient(); Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/admin/logs?issueCode=" + code)).StatusCode);
    }

    [Fact]
    public async Task QueryDetailExportPermissionsAreIndependentAndReadsAreAudited()
    {
        await using var factory = new NexusFactory(administrators: ["alice"]); using var alice = await factory.SignedInAsync();
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>(); var code = Issues.NewCode();
        var row = new DiagnosticEvent { IssueCode = code, Level = LogLevel.Error, ExceptionType = "InvalidOperationException", ExceptionDetail = "[message omitted]", Category = "=HYPERLINK(\"evil\")", At = DateTimeOffset.UtcNow.AddMinutes(-1) }; db.Add(row); await db.SaveChangesAsync();
        var range = Range(); var page = await alice.GetFromJsonAsync<DiagnosticPage>("/api/v1/admin/logs?" + range + "&issueCode=" + code); Assert.Single(page!.Events);
        var json = await (await alice.GetAsync("/api/v1/admin/logs?" + range)).Content.ReadAsStringAsync(); Assert.DoesNotContain("exceptionDetail", json); Assert.DoesNotContain("ExceptionDetail", json);
        var detail = await alice.GetFromJsonAsync<DiagnosticDetail>("/api/v1/admin/logs/" + row.LogId); Assert.Equal(row.ExceptionDetail, detail!.ExceptionDetail);
        var exported = await alice.GetAsync("/api/v1/admin/logs/export?" + range); exported.EnsureSuccessStatusCode(); Assert.Contains("'=HYPERLINK", await exported.Content.ReadAsStringAsync());
        db.RemoveRange(await db.Set<RoleGroupFeature>().Where(x => x.GroupId == "administrators" && (x.FeatureId == DiagnosticConfiguration.Detail || x.FeatureId == DiagnosticConfiguration.Export)).ToListAsync()); await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await alice.GetAsync("/api/v1/admin/logs/" + row.LogId)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await alice.GetAsync("/api/v1/admin/logs/export?" + range)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await alice.GetAsync("/api/v1/admin/logs?" + range)).StatusCode);
        Assert.Contains(await db.AuditEvents.AsNoTracking().ToArrayAsync(), x => x.Action == "logs.export");
        Assert.Contains(await db.AuditEvents.AsNoTracking().ToArrayAsync(), x => x.Action == "logs.detail");
    }

    [Fact]
    public async Task CursorIsBoundToActorFiltersAndTimeWindowAndHasNoDuplicates()
    {
        await using var factory = new NexusFactory(administrators: ["alice", "bob"]); using var alice = await factory.SignedInAsync(); using var bob = await factory.SignedInAsync("bob");
        var code = Issues.NewCode(); using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        var at = DateTimeOffset.UtcNow.AddMinutes(-1); db.AddRange(Enumerable.Range(0, 7).Select(i => new DiagnosticEvent { IssueCode = code, At = at.AddSeconds(i), Level = LogLevel.Error })); await db.SaveChangesAsync();
        var query = "/api/v1/admin/logs?" + Range() + "&issueCode=" + code + "&take=3";
        var first = await alice.GetFromJsonAsync<DiagnosticPage>(query); var second = await alice.GetFromJsonAsync<DiagnosticPage>(query + "&cursor=" + Uri.EscapeDataString(first!.NextCursor!));
        Assert.Equal(3, first.Events.Count); Assert.Equal(3, second!.Events.Count); Assert.Empty(first.Events.Select(x => x.LogId).Intersect(second.Events.Select(x => x.LogId)));
        Assert.Equal(HttpStatusCode.BadRequest, (await bob.GetAsync(query + "&cursor=" + Uri.EscapeDataString(first.NextCursor!))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await alice.GetAsync(query + "&level=Warning&cursor=" + Uri.EscapeDataString(first.NextCursor!))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await alice.GetAsync("/api/v1/admin/logs?take=100000")).StatusCode);
    }

    [Fact]
    public async Task GenerationFailureProducesSafeSseNotificationAndQueryableCorrelatedIssue()
    {
        await using var factory = new NexusFactory(administrators: ["alice"]); factory.Provider.Fail = true;
        using var client = await factory.SignedInAsync(); var conversation = await CreateConversation(client);
        using var response = await PostRun(client, new CreateRunRequest(conversation.Id, "test-model", Secret, null, null), Guid.NewGuid().ToString()); response.EnsureSuccessStatusCode();
        var run = (await response.Content.ReadFromJsonAsync<RunDto>())!; var terminal = await WaitForTerminal(client, run.Id); Assert.Equal("failed", terminal.Status); Assert.True(Issues.ValidCode(terminal.IssueCode));
        var stream = await client.GetStringAsync($"/api/v1/runs/{run.Id}/events"); Assert.Contains(terminal.IssueCode!, stream); Assert.DoesNotContain("fixture failure", stream); Assert.DoesNotContain(Secret, stream);
        var notifications = await client.GetStringAsync("/api/v1/notifications"); Assert.Contains(terminal.IssueCode!, notifications); Assert.DoesNotContain("fixture failure", notifications);
        var item = await WaitForIssue(factory, terminal.IssueCode!); Assert.Equal(run.Id, item.RunId); Assert.Equal(run.Id, item.OperationId); Assert.NotNull(item.TraceId); Assert.DoesNotContain("fixture failure", item.ExceptionDetail ?? "");
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>(); var persisted = await db.Runs.FindAsync(run.Id); Assert.Equal(persisted!.TraceId, item.TraceId);
        Assert.True(await db.Set<DiagnosticEvent>().AnyAsync(x => x.TraceId == item.TraceId && x.RequestId != null));
        Assert.Equal(1, await CountIssue(factory, terminal.IssueCode!));
    }

    [Fact]
    public async Task BackgroundRetryKeepsTraceAndJobButGetsDistinctIssueAndAttempt()
    {
        await using var factory = new NexusFactory(backgroundJobs: false, administrators: ["alice"], services: services => services.AddScoped<IBackgroundJobHandler, FailingJob>());
        using var client = await factory.SignedInAsync(); var owner = (await client.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id; Guid jobId;
        using (var scope = factory.Services.CreateScope()) { using var trace = DiagnosticTrace.Start("test.enqueue"); var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>(); var job = scope.ServiceProvider.GetRequiredService<JobService>().Enqueue(owner, null, Guid.NewGuid(), "diagnostic-fixture", "safe task"); jobId = job.Id; await db.SaveChangesAsync(); }
        using var worker = ActivatorUtilities.CreateInstance<BackgroundJobWorker>(factory.Services); Assert.True(await worker.ProcessNextAsync(CancellationToken.None));
        var first = await client.GetFromJsonAsync<JobDto>("/api/v1/jobs/" + jobId); Assert.True(Issues.ValidCode(first!.IssueCode)); Assert.DoesNotContain(Secret, first.ErrorMessage!);
        (await client.PostAsync("/api/v1/jobs/" + jobId + "/retry", null)).EnsureSuccessStatusCode(); Assert.True(await worker.ProcessNextAsync(CancellationToken.None));
        var second = await client.GetFromJsonAsync<JobDto>("/api/v1/jobs/" + jobId); Assert.NotEqual(first.IssueCode, second!.IssueCode);
        var a = await WaitForIssue(factory, first.IssueCode!); var b = await WaitForIssue(factory, second.IssueCode!); Assert.Equal(a.TraceId, b.TraceId); Assert.NotEqual(a.SpanId, b.SpanId); Assert.Equal(jobId, a.JobId); Assert.Equal(1, a.Attempt); Assert.Equal(2, b.Attempt);
    }
    public sealed class FailingJob : IBackgroundJobHandler
    {
        public string Kind => "diagnostic-fixture";
        public Task ExecuteAsync(JobExecution execution, CancellationToken ct) => throw new ApiException(503, "fixture_failed", Secret);
        public Task ValidateRetryAsync(BackgroundJob job, CancellationToken ct) => Task.CompletedTask;
    }

    [Fact]
    public async Task ApiFrameworkFailureDoesNotReflectClientDataAndUsesServerGeneratedCorrelation()
    {
        await using var factory = new NexusFactory(); using var client = await factory.SignedInAsync();
        client.DefaultRequestHeaders.Add("traceparent", "00-" + new string('a', 32) + "-" + new string('b', 16) + "-01");
        client.DefaultRequestHeaders.Add("X-Issue-Code", "NX-" + new string('A', 32));
        var response = await client.PostAsync("/api/v1/conversations", new StringContent("{malformed " + Secret, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); var text = await response.Content.ReadAsStringAsync(); Assert.DoesNotContain(Secret, text);
        using var parsed = JsonDocument.Parse(text); var code = parsed.RootElement.GetProperty("issueCode").GetString()!; Assert.True(Issues.ValidCode(code)); Assert.NotEqual("NX-" + new string('A', 32), code);
        var item = await WaitForIssue(factory, code); Assert.NotEqual(new string('a', 32), item.TraceId); Assert.Equal(LogLevel.Information, item.Level);
    }

    [Fact]
    public async Task OpenApiContainsDiagnosticContractsAndCanBeExportedWithoutMachineSecrets()
    {
        await using var factory = new NexusFactory(); using var client = factory.CreateClient(); var contract = await client.GetStringAsync("/openapi/v1.json");
        Assert.Contains("QuerySystemLogs", contract); Assert.Contains("DiagnosticHealthDto", contract); Assert.Contains("issueCode", contract);
        if (Environment.GetEnvironmentVariable("NEXUS_OPENAPI_OUTPUT") is { Length: > 0 } path) await File.WriteAllTextAsync(path, contract);
    }

    private static string Range() => "from=" + Uri.EscapeDataString(DateTimeOffset.UtcNow.AddHours(-1).ToString("O")) + "&to=" + Uri.EscapeDataString(DateTimeOffset.UtcNow.AddMinutes(1).ToString("O"));
    private static async Task<int> CountIssue(NexusFactory factory, string code) { using var scope = factory.Services.CreateScope(); return await scope.ServiceProvider.GetRequiredService<NexusDbContext>().Set<DiagnosticEvent>().CountAsync(x => x.IssueCode == code); }
    internal static async Task<DiagnosticEvent> WaitForIssue(NexusFactory factory, string code)
    {
        for (var attempt = 0; attempt < 200; attempt++) {
            using var scope = factory.Services.CreateScope(); var item = await scope.ServiceProvider.GetRequiredService<NexusDbContext>().Set<DiagnosticEvent>().AsNoTracking().FirstOrDefaultAsync(x => x.IssueCode == code);
            if (item is not null) return item; await Task.Delay(25);
        }
        throw new Xunit.Sdk.XunitException("Issue was not replayed to persistent storage: " + code);
    }
    private sealed class MemoryStore : IDiagnosticStore
    {
        public bool Offline { get; set; }
        public Dictionary<Guid, DiagnosticEvent> Events { get; } = [];
        public Task WriteAsync(IReadOnlyList<DiagnosticEvent> events, CancellationToken ct) { if (Offline) throw new IOException("SQL offline fixture"); foreach (var item in events) Events.TryAdd(item.LogId, item); return Task.CompletedTask; }
        public Task CleanupAsync(CancellationToken ct) => Task.CompletedTask;
    }
    private sealed class EnvironmentFixture : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "AiNexus";
        public string ContentRootPath { get; set; } = Path.Combine(Path.GetTempPath(), "nexus-deployment-fixture");
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
