using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.AccessControl;
using AiNexus.Features.Identity;
using AiNexus.Features.Monitoring;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;
using AiNexus.Features.Persistence;

namespace AiNexus.Tests;

public sealed class MonitoringTests
{
    [Theory]
    [InlineData("/api/v1/admin/audit", "audit")]
    [InlineData("/api/v1/admin/audit/catalog", "audit")]
    [InlineData("/api/v1/admin/logs/{id:guid}", "logs")]
    [InlineData("/api/v1/admin/monitoring/snapshot", "monitoring")]
    [InlineData("/api/v1/admin/audit-other", "admin")]
    public void InvestigationRoutesUseTheirOwnFixedFeature(string route, string expected)
    {
        Assert.Equal(expected, MonitoringVocabulary.Feature(route));
        Assert.True(MonitoringVocabulary.ValidFeature(expected));
    }

    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset at = new(2026, 10, 8, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => at;
        public void Advance(int seconds) => at = at.AddSeconds(seconds);
    }
    private static RuntimeTraffic Traffic(Clock clock, int capacity = 2000) => new(Options.Create(new MonitoringOptions { MaxSessions = capacity }), clock);
    private static NexusUser User(string name = "Alice") => new() { DisplayName = name, Account = "CORP\\" + name };
    private static void Heartbeat(RuntimeTraffic traffic, NexusUser user, Guid tab, string state = "active") => traffic.Presence(user, new(tab, "chat", state), "10.1.2.3", "Edge", "Windows", "ad", false);

    [Fact]
    public void PresenceCountsPeopleSeparatelyFromTabsAndExpiresWithoutReads()
    {
        var clock = new Clock(); var traffic = Traffic(clock); var user = User();
        var first = Guid.NewGuid(); var second = Guid.NewGuid();
        Heartbeat(traffic, user, first); Heartbeat(traffic, user, second, "background");
        Assert.Equal(1, traffic.Snapshot(5).OnlineUsers); Assert.Equal(2, traffic.Snapshot(5).OnlineSessions);
        clock.Advance(70); Heartbeat(traffic, user, first, "idle"); clock.Advance(21);
        var snapshot = traffic.Snapshot(5);
        Assert.Single(snapshot.Sessions); Assert.Equal("idle", snapshot.Sessions[0].State);
        traffic.Leave(user.Id, first); Assert.Empty(traffic.Snapshot(5).Sessions);
    }
    [Fact]
    public void IdentityOwnsSessionKeyAndCapacityDoesNotEvictExistingUsers()
    {
        var clock = new Clock(); var traffic = Traffic(clock, 2); var tab = Guid.NewGuid();
        var a = User(); var b = User("Bob"); var c = User("Carol");
        Heartbeat(traffic, a, tab); Heartbeat(traffic, b, tab); Heartbeat(traffic, c, tab);
        var snapshot = traffic.Snapshot(1); Assert.Equal(2, snapshot.OnlineUsers); Assert.Equal(1, snapshot.DroppedSessions);
        traffic.Leave(c.Id, tab); Assert.Equal(2, traffic.Snapshot(1).OnlineSessions);
    }
    [Fact]
    public void RollingMetricsExcludeOpenBucketAndStreamingLatencyButCountBodyBytes()
    {
        var clock = new Clock(); var traffic = Traffic(clock);
        traffic.RequestStarted(false); traffic.Transferred(120, 180); traffic.RequestFinished(null, null, "POST", "/api/v1/runs", 200, 30, 120, 180, false, false, null);
        traffic.RequestStarted(true); traffic.Transferred(0, 2000); traffic.RequestFinished(null, null, "GET", "/api/v1/runs/{id}/events", 200, 20000, 0, 2000, true, false, null);
        Assert.Equal(0, traffic.Snapshot(1).Traffic.Requests);
        clock.Advance(10); var snapshot = traffic.Snapshot(1);
        Assert.Equal(2, snapshot.Traffic.Requests); Assert.Equal(.2, snapshot.Traffic.RequestsPerSecond);
        Assert.Equal(30, snapshot.Traffic.AverageMs); Assert.Equal(30, snapshot.Traffic.P95Ms);
        Assert.Equal(2180, snapshot.Traffic.SentBytes); Assert.Equal(120, snapshot.Traffic.ReceivedBytes);
        Assert.Equal(0, snapshot.InFlightRequests); Assert.Equal(0, snapshot.OpenStreams);
        clock.Advance(901); Assert.Equal(0, traffic.Snapshot(15).Traffic.Requests); Assert.Empty(traffic.Snapshot(15).Activities);
    }
    [Fact]
    public void StreamingBytesAreVisibleBeforeRequestCompletes()
    {
        var clock = new Clock(); using var traffic = Traffic(clock);
        traffic.RequestStarted(true); traffic.Transferred(0, 4096); clock.Advance(10);
        var snapshot = traffic.Snapshot(1);
        Assert.Equal(4096, snapshot.Traffic.SentBytes); Assert.Equal(0, snapshot.Traffic.Requests);
        Assert.Equal(1, snapshot.OpenStreams);
        traffic.RequestFinished(null, null, "GET", "/api/v1/runs/{id}/events", 200, 10000, 0, 4096, true, false, null);
        clock.Advance(10); Assert.Equal(4096, traffic.Snapshot(1).Traffic.SentBytes);
    }
    [Fact]
    public void CancelledRequestsAreSeparateFromErrorsAndActivityRetentionIsBounded()
    {
        var clock = new Clock(); var traffic = Traffic(clock);
        for (var i = 0; i < 500; i++) {
            traffic.RequestStarted(false); traffic.RequestFinished(null, null, "POST", "/api/v1/runs", 503, 10, 0, 0, false, true, null);
        }
        clock.Advance(10); var snapshot = traffic.Snapshot(1);
        Assert.Equal(500, snapshot.Traffic.Cancelled); Assert.Equal(0, snapshot.Traffic.Errors);
        Assert.Equal(0, snapshot.Traffic.ServerErrors);
        Assert.Equal(120, snapshot.Activities.Length);
    }
    [Fact]
    public void SqlObserverReadsOnlyConnectionClassificationAndTracksFailures()
    {
        var clock = new Clock(); var traffic = Traffic(clock);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Nexus"] = "Server=fixture;Database=fixture-db;Integrated Security=True" }).Build();
        var catalog = new DependencyCatalog(config, traffic);
        var observer = new RuntimeSampler(traffic, catalog, clock);
        using var connection = new SqlConnection(config.GetConnectionString("Nexus"));
        using var command = new SqlCommand("SELECT 'private-password-prompt'", connection);
        var operation = Guid.NewGuid();
        observer.OnNext(new KeyValuePair<string, object?>("Microsoft.Data.SqlClient.WriteCommandBefore", new { OperationId = operation, Command = command }));
        Assert.Equal(1, traffic.Snapshot(1).Dependencies.Single(x => x.Id == "sql.nexus").InFlight);
        observer.OnNext(new KeyValuePair<string, object?>("Microsoft.Data.SqlClient.WriteCommandError", new { OperationId = operation, Exception = new Exception("private") }));
        clock.Advance(10); var dep = traffic.Snapshot(1).Dependencies.Single(x => x.Id == "sql.nexus");
        Assert.Equal("failing", dep.Status); Assert.Equal(1, dep.Metrics.Errors); Assert.Equal(0, dep.InFlight);
        Assert.DoesNotContain("private", System.Text.Json.JsonSerializer.Serialize(traffic.Snapshot(1)));
    }
    [Fact]
    public async Task FactoryClientsShareDependencyObservationAndSuppressObserverCalls()
    {
        var clock = new Clock();
        await using var factory = new NexusFactory(services: services => {
            services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(clock);
            services.AddHttpClient("monitoring-probe").ConfigurePrimaryHttpMessageHandler(() => new ProbeHandler());
        });
        using var host = factory.CreateClient();
        using var client = factory.Services.GetRequiredService<IHttpClientFactory>().CreateClient("monitoring-probe");
        using var failed = await client.GetAsync("https://generativelanguage.googleapis.com/private?key=private");
        Assert.Equal(HttpStatusCode.BadGateway, failed.StatusCode);
        using (var suppression = new MonitoringSuppression()) {
            using var ignored = await client.GetAsync("https://generativelanguage.googleapis.com/observer");
        }
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetAsync("https://generativelanguage.googleapis.com/cancelled", cancelled.Token));
        clock.Advance(10);
        var snapshot = factory.Services.GetRequiredService<RuntimeTraffic>().Snapshot(1);
        var dependency = Assert.Single(snapshot.Dependencies, d => d.Id == "google");
        Assert.Equal(2, dependency.Metrics.Requests); Assert.Equal(1, dependency.Metrics.Errors);
        Assert.Equal(1, dependency.Metrics.Cancelled); Assert.Equal(0, dependency.InFlight);
        Assert.DoesNotContain("private", System.Text.Json.JsonSerializer.Serialize(snapshot));
    }
    private sealed class ProbeHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway));
        }
    }
    [Fact]
    public async Task PresenceIsAuthenticatedCsrfProtectedAndMonitorIsSeparatelyGranted()
    {
        await using var factory = new NexusFactory(administrators: ["alice"]);
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/v1/presence", new PresenceRequest(Guid.NewGuid(), "chat", "active"))).StatusCode);
        using var bob = await factory.SignedInAsync("bob"); using var alice = await factory.SignedInAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await bob.GetAsync("/api/v1/admin/monitoring")).StatusCode);
        var tab = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.OK, (await bob.PostAsJsonAsync("/api/v1/presence", new PresenceRequest(tab, "chat", "active"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await bob.PostAsJsonAsync("/api/v1/presence", new PresenceRequest(tab, "/chat/private-title?secret=1", "active"))).StatusCode);
        var snapshot = (await alice.GetFromJsonAsync<MonitoringSnapshot>("/api/v1/admin/monitoring"))!;
        Assert.Equal("bob", Assert.Single(snapshot.Sessions).DisplayName);
        bob.DefaultRequestHeaders.Remove("X-Nexus-CSRF");
        Assert.Equal(HttpStatusCode.Forbidden, (await bob.PostAsJsonAsync("/api/v1/presence", new PresenceRequest(tab, "chat", "active"))).StatusCode);
    }
    [Fact]
    public async Task SharedPipelineCountsRealPayloadAndExcludesMonitoringTraffic()
    {
        var clock = new Clock();
        await using var factory = new NexusFactory(administrators: ["alice"], services: services => {
            services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(clock);
        });
        using var alice = await factory.SignedInAsync();
        var tab = Guid.NewGuid(); alice.DefaultRequestHeaders.Add("X-Nexus-Session", tab.ToString());
        await alice.PostAsJsonAsync("/api/v1/presence", new PresenceRequest(tab, "chat", "active"));
        var response = await alice.PostAsJsonAsync("/api/v1/conversations", new { }); response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsByteArrayAsync();
        await alice.GetAsync("/api/v1/admin/monitoring"); await alice.GetAsync("/api/v1/admin/monitoring");
        clock.Advance(10);
        var snapshot = (await alice.GetFromJsonAsync<MonitoringSnapshot>("/api/v1/admin/monitoring"))!;
        var session = Assert.Single(snapshot.Sessions); Assert.Equal(1, session.Requests);
        Assert.Equal(body.Length, session.SentBytes); Assert.True(session.ReceivedBytes > 0);
        Assert.DoesNotContain(snapshot.Endpoints, e => e.Route.Contains("presence") || e.Route.Contains("monitoring"));
        Assert.NotNull(Assert.Single(snapshot.Activities, x => x.SessionId == session.Id).TraceId);
    }
    [Fact]
    public async Task ExportIsAuditedAndDisabledTelemetryRemainsEmpty()
    {
        await using var factory = new NexusFactory(administrators: ["alice"], services: services => services.PostConfigure<MonitoringOptions>(x => x.Enabled = false));
        using var alice = await factory.SignedInAsync();
        await alice.PostAsJsonAsync("/api/v1/presence", new PresenceRequest(Guid.NewGuid(), "chat", "active"));
        var export = await alice.GetAsync("/api/v1/admin/monitoring/export?minutes=15"); export.EnsureSuccessStatusCode();
        var snapshot = (await export.Content.ReadFromJsonAsync<MonitoringSnapshot>())!;
        Assert.False(snapshot.Enabled); Assert.Empty(snapshot.Sessions); Assert.Equal(0, snapshot.Traffic.Requests);
        using var scope = factory.Services.CreateScope();
        Assert.Contains(scope.ServiceProvider.GetRequiredService<NexusDbContext>().AuditEvents, x => x.Action == "monitoring.export");
    }
    [Fact]
    public async Task RevokedGrantTerminatesExistingStream()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = new NexusFactory(administrators: ["alice"], clock: clock);
        using var alice = await factory.SignedInAsync(); using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancel.CancelAfter(TimeSpan.FromSeconds(10));
        using var response = await alice.GetAsync("/api/v1/admin/monitoring/events", HttpCompletionOption.ResponseHeadersRead, cancel.Token);
        response.EnsureSuccessStatusCode();
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(cancel.Token));
        Assert.Equal("event: snapshot", await reader.ReadLineAsync(cancel.Token));
        using (var scope = factory.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
            db.Remove(db.Set<RoleGroupFeature>().Single(x => x.FeatureId == "monitoring" && x.GroupId == "administrators")); await db.SaveChangesAsync(cancel.Token);
        }
        // Grants are re-checked every 15 seconds of stream time.
        clock.Advance(TimeSpan.FromSeconds(15));
        var ended = false;
        while (!cancel.IsCancellationRequested) {
            var line = await reader.ReadLineAsync(cancel.Token);
            if (line is null) break;
            if (line == "event: error") { ended = true; break; }
        }
        Assert.True(ended);
    }
}
