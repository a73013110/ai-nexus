using System.Net.Http.Json;
using AiNexus.Features.AccessControl.Roles;
using AiNexus.Features.Jobs;
using AiNexus.Features.Monitoring;
using AiNexus.Features.Monitoring.Presence;
using AiNexus.Features.Monitoring.Snapshots;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Monitoring;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace AiNexus.IntegrationTests.Monitoring;

public sealed class MonitoringSnapshotTests
{
    [Fact]
    public async Task SharedPipelineCountsRealPayloadAndExcludesMonitoringTraffic()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 8, 0, 0, 0, TimeSpan.Zero));
        await using var factory = new NexusFactory(administrators: ["alice"], services: services => {
            services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(clock);
        });
        using var alice = await factory.SignedInAsync();
        var tab = Guid.NewGuid(); alice.DefaultRequestHeaders.Add("X-Nexus-Session", tab.ToString());
        await alice.PostAsJsonAsync("/api/v1/presence", new PresenceRequest(tab, "chat", "active"));
        var response = await alice.PostAsJsonAsync("/api/v1/conversations", new { }); response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsByteArrayAsync();
        await alice.GetAsync("/api/v1/admin/monitoring"); await alice.GetAsync("/api/v1/admin/monitoring");
        clock.Advance(TimeSpan.FromSeconds(10));
        var snapshot = (await alice.GetFromJsonAsync<MonitoringSnapshot>("/api/v1/admin/monitoring"))!;
        var session = Assert.Single(snapshot.Sessions); Assert.Equal(1, session.Requests);
        Assert.Equal(body.Length, session.SentBytes); Assert.True(session.ReceivedBytes > 0);
        Assert.DoesNotContain(snapshot.Endpoints, e => e.Route.Contains("presence") || e.Route.Contains("monitoring"));
        Assert.NotNull(Assert.Single(snapshot.Activities, x => x.SessionId == session.Id).TraceId);
    }

    [Fact]
    public async Task SnapshotReportsHealthAndEveryModulesSubsystemAfterABackgroundRead()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = new NexusFactory(administrators: ["alice"], clock: clock);
        using var alice = await factory.SignedInAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
            db.Set<BackgroundJob>().Add(new() { OwnerId = db.Users.Select(x => x.Id).First(), Kind = "document-ingest", Status = "running", LeaseToken = Guid.NewGuid(),
                LeaseUntil = clock.GetUtcNow().AddMinutes(-1), OperationId = Guid.NewGuid(), SubjectId = Guid.NewGuid() });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        var first = (await alice.GetFromJsonAsync<MonitoringSnapshot>("/api/v1/admin/monitoring", TestContext.Current.CancellationToken))!;
        Assert.Equal(2, first.Version);
        Assert.Equal(["generation", "model-providers", "jobs", "log-pipeline", "knowledge-index", "usage-today", "storage", "integrations"], first.Subsystems.Select(x => x.Id));
        // Reads run in the background; the page gets them with the next snapshot.
        await factory.Services.GetRequiredService<SubsystemReadings>().Settled;
        clock.Advance(MonitoringSnapshots.ReuseFor);
        var snapshot = (await alice.GetFromJsonAsync<MonitoringSnapshot>("/api/v1/admin/monitoring", TestContext.Current.CancellationToken))!;
        Assert.All(snapshot.Subsystems, x => Assert.NotNull(x.ObservedAt));
        Assert.DoesNotContain(snapshot.Subsystems, x => x.Summary == SubsystemReadings.UnreadableSummary);
        var jobs = Assert.Single(snapshot.Subsystems, x => x.Id == "jobs");
        Assert.Equal(SubsystemStates.Degraded, jobs.Status);
        Assert.Equal(1, Assert.Single(jobs.Facts, x => x.Id == "expired_leases").Value);
        Assert.Equal(SubsystemStates.Healthy, Assert.Single(snapshot.Subsystems, x => x.Id == "log-pipeline").Status);
        Assert.Equal(MonitoringHealth.Attention, snapshot.Health.Status);
        Assert.Contains(snapshot.Health.Issues, x => x is { Code: "subsystem_degraded", Subject: "jobs" });
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
    public async Task NewStreamSupersedesTheOldestInsteadOfBeingRefused()
    {
        await using var factory = new NexusFactory(administrators: ["alice"]);
        using var alice = await factory.SignedInAsync(); using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancel.CancelAfter(TimeSpan.FromSeconds(20));
        var responses = new List<HttpResponseMessage>(); var readers = new List<StreamReader>();
        try
        {
            // A reload leaves the old streams open until the server notices; the next page must still connect.
            for (var i = 0; i <= MonitoringStreams.PerUser; i++)
            {
                var response = await alice.GetAsync("/api/v1/admin/monitoring/events", HttpCompletionOption.ResponseHeadersRead, cancel.Token);
                response.EnsureSuccessStatusCode(); responses.Add(response);
                var reader = new StreamReader(await response.Content.ReadAsStreamAsync(cancel.Token)); readers.Add(reader);
                Assert.Equal("event: snapshot", await reader.ReadLineAsync(cancel.Token));
            }
            string? line;
            while ((line = await readers[0].ReadLineAsync(cancel.Token)) is not null && line != "event: error") { }
            Assert.Equal("event: error", line);
            Assert.Contains("\"stream_superseded\"", await readers[0].ReadLineAsync(cancel.Token));
        }
        finally { readers.ForEach(x => x.Dispose()); responses.ForEach(x => x.Dispose()); }
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
