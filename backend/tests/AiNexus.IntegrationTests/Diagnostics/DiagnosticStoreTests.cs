using System.Net.Http.Json;
using System.Text.Json;
using AiNexus.Features.Account;
using AiNexus.Features.Diagnostics;
using AiNexus.Features.Identity.Users;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiNexus.IntegrationTests.Diagnostics;

public sealed class DiagnosticStoreTests
{
    private const string Secret = "private-provider-password-123";

    [Fact]
    public async Task AuditBoundaryFailsClosedMasksLegacyAndShowsSystemConfiguration()
    {
        Assert.Throws<InvalidOperationException>(() => AuditRedactor.Sanitize("{"));
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

    [Fact, Trait("Category", "SqlServer")]
    public async Task SqlServerBulkImportIsIdempotentIndependentAndRetentionIsBounded()
    {
        await SqlServerDatabase.WithDatabase(async db => {
            var services = new ServiceCollection();
            services.AddDbContext<NexusDbContext>(o => o.UseSqlServer(db.Database.GetConnectionString()!));
            using var provider = services.BuildServiceProvider(); using var health = new DiagnosticHealth();
            var options = Options.Create(new DiagnosticOptions { CleanupBatchSize = 2 });
            var store = new DiagnosticStore(provider.GetRequiredService<IServiceScopeFactory>(), options, health, TimeProvider.System);
            var code = Issues.NewCode();
            var row = new DiagnosticEvent { IssueCode = code, Level = LogLevel.Error, At = DateTimeOffset.UtcNow, TraceId = new string('a', 32) };
            var user = new NexusUser { Sid = "diagnostic-rollback", Account = "rollback", DisplayName = "fixture" };
            await using (var transaction = await db.Database.BeginTransactionAsync()) {
                db.Add(user); await db.SaveChangesAsync(); await store.WriteAsync([row, row], CancellationToken.None); await transaction.RollbackAsync();
            }
            await store.WriteAsync([row], CancellationToken.None);
            Assert.False(await db.Users.AsNoTracking().AnyAsync(x => x.Id == user.Id));
            Assert.Equal(1, await db.Set<DiagnosticEvent>().CountAsync(x => x.IssueCode == code));
            Assert.Equal(row.TraceId, (await db.Set<DiagnosticEvent>().AsNoTracking().SingleAsync(x => x.LogId == row.LogId)).TraceId);
            await store.WriteAsync(Enumerable.Range(0, 5).Select(_ => new DiagnosticEvent { At = DateTimeOffset.UtcNow.AddDays(-31) }).ToArray(), CancellationToken.None);
            await store.CleanupAsync(CancellationToken.None);
            Assert.Equal(1, await db.Set<DiagnosticEvent>().CountAsync());
        });
    }
}
