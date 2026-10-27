using AiNexus.Data.App;
using AiNexus.Data.App.Audit;
using AiNexus.Data.App.Identity;
using System.Net.Http.Json;
using System.Text.Json;
using AiNexus.Features.Platform.Account;
using AiNexus.Features.Admin.Diagnostics;
using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Auditing;
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
        await using var factory = new AppFactory(administrators: ["alice"]); using var client = await factory.SignedInAsync();
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var actor = (await client.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        var legacy = new AuditEvent { Category = "activity", OwnerId = actor, Action = "fixture.legacy.updated" }; db.Add(legacy); await db.SaveChangesAsync();
        var raw = JsonSerializer.Serialize(new { password = Secret, before = new { token = Secret, enabled = true }, reason = Secret });
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [audit].[AuditEvents] SET [DetailsJson] = {raw} WHERE [Id] = {legacy.Id}");
        var response = await client.GetStringAsync("/api/v1/admin/audit?action=fixture.legacy."); Assert.DoesNotContain(Secret, response);
        await factory.Services.GetRequiredService<IDiagnosticStore>().CleanupAsync(CancellationToken.None);
        var system = await client.GetStringAsync($"/api/v1/admin/audit?action={DiagnosticsAudit.ConfigurationActivated.Name}"); Assert.Contains("system", system); Assert.Contains("fingerprint", system);
        Assert.DoesNotContain("ainexus-test-", system); Assert.DoesNotContain("localhost:4318", system);
    }

    [Fact]
    public async Task SqlCapacityAllowsDuplicateReplayAndRetentionKeepsDistinctAuditPolicy()
    {
        await using var factory = new AppFactory(services: services => services.PostConfigure<DiagnosticOptions>(x => { x.MaxSqlRows = 1000; x.CleanupBatchSize = 10; }));
        using var client = factory.CreateClient(); (await client.GetAsync("/health/live")).EnsureSuccessStatusCode();
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var old = Enumerable.Range(0, 1000).Select(_ => new DiagnosticEvent { At = DateTimeOffset.UtcNow.AddDays(-31), Level = LogLevel.Information }).ToArray();
        db.AddRange(old); var retained = new AuditEvent { Category = "activity", Action = "fixture.retained", At = DateTimeOffset.UtcNow.AddDays(-31) }; var expired = new AuditEvent { Category = "activity", Action = "fixture.expired", At = DateTimeOffset.UtcNow.AddDays(-366) };
        db.AddRange(retained, expired); await db.SaveChangesAsync(); var store = factory.Services.GetRequiredService<IDiagnosticStore>();
        await store.WriteAsync([old[0]], CancellationToken.None); // 重複寫入是安全的，即使已超過軟性容量上限。
        await Assert.ThrowsAsync<DiagnosticCapacityException>(() => store.WriteAsync([new DiagnosticEvent()], CancellationToken.None));
        await store.CleanupAsync(CancellationToken.None);
        Assert.False(await db.Set<DiagnosticEvent>().AnyAsync(x => x.LogId == old[0].LogId)); Assert.True(await db.AuditEvents.AnyAsync(x => x.Id == retained.Id));
        Assert.False(await db.AuditEvents.AnyAsync(x => x.Id == expired.Id)); await store.WriteAsync([new DiagnosticEvent()], CancellationToken.None);
    }

    [Fact]
    public async Task SqlServerBulkImportIsIdempotentIndependentAndRetentionIsBounded()
    {
        await TestDatabase.WithContextAsync(async db => {
            var services = new ServiceCollection();
            services.AddDbContext<AppDbContext>(o => o.UseSqlServer(db.Database.GetConnectionString()!));
            services.AddSingleton(TimeProvider.System).AddSingleton<AuditContextSource>().AddSingleton<RepeatedReads>().AddScoped<AuditTrail>();
            using var provider = services.BuildServiceProvider(); using var health = new DiagnosticHealth();
            var options = Options.Create(new DiagnosticOptions { CleanupBatchSize = 2 });
            var store = new DiagnosticStore(provider.GetRequiredService<IServiceScopeFactory>(), options, health, TimeProvider.System);
            var code = Issues.NewCode();
            var row = new DiagnosticEvent { IssueCode = code, Level = LogLevel.Error, At = DateTimeOffset.UtcNow, TraceId = new string('a', 32) };
            var user = new User { Sid = "diagnostic-rollback", AdAccount = "rollback", DisplayName = "fixture" };
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
