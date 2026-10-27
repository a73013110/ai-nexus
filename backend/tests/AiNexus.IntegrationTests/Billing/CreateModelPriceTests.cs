using AiNexus.Data.App;
using AiNexus.Data.App.Audit;
using AiNexus.Data.App.Billing;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AiNexus.Features.Platform.Account;
using AiNexus.Features.Admin.Billing.Prices;
using AiNexus.Platform.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace AiNexus.IntegrationTests.Billing;

/// <summary>經端點在 LocalDB 實際呼叫 billing.usp_CreateModelPrice：交易、事件與請求稽核一起提交或回復。</summary>
public sealed class CreateModelPriceTests
{
    private const string Action = "billing.price.created";

    [Fact]
    public async Task ProcedureWritesPriceAndTheRequestAuditAndEventCommitInTheSameTransaction()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var events = new PriceEvents();
        await using var factory = new AppFactory(administrators: ["alice"], clock: clock, services: events.Register);
        using var alice = await factory.SignedInAsync();
        var aliceId = (await alice.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        var request = new PriceRequest("google", "test-model", "USD", "api", 1, .5m, 2, 0, "completed", clock.GetUtcNow().AddDays(1), "  fixture  ");

        var response = await alice.PostAsJsonAsync("/api/v1/admin/billing/prices", request); response.EnsureSuccessStatusCode();
        var price = (await response.Content.ReadFromJsonAsync<PriceDto>())!;
        Assert.Equal(("fixture", "測試模型", request.EffectiveAt), (price.Note, price.ModelDisplayName, price.EffectiveAt));
        // 事件處理器在提交前執行，看得到 procedure 尚未提交的資料列。
        Assert.Equal([(price.Id, true)], events.Seen);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var audit = await db.AuditEvents.SingleAsync(x => x.Action == Action);
        Assert.Equal((aliceId, AuditEvent.Completed, (Guid?)price.Id, "administration", clock.GetUtcNow()), (audit.OwnerId, audit.Result, audit.ResourceId, audit.Category, audit.At));
        using var details = JsonDocument.Parse(audit.DetailsJson!);
        Assert.Equal("test-model", details.RootElement.GetProperty("modelId").GetString());
        var row = await db.Set<ModelPrice>().SingleAsync();
        Assert.Equal((aliceId, clock.GetUtcNow()), (row.CreatedBy, row.CreatedAt));
    }

    [Fact]
    public async Task DuplicateIsRejectedByTheProcedureAndAuditedAsFailure()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = new AppFactory(administrators: ["alice"], clock: clock);
        using var alice = await factory.SignedInAsync();
        var aliceId = (await alice.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        var request = new PriceRequest("google", "test-model", "USD", "api", 1, 1, 2, 0, "completed", clock.GetUtcNow(), "fixture");
        (await alice.PostAsJsonAsync("/api/v1/admin/billing/prices", request)).EnsureSuccessStatusCode();

        var duplicate = await alice.PostAsJsonAsync("/api/v1/admin/billing/prices", request with { PerRequest = 9 });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Contains("price_exists", await duplicate.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(0, (await db.Set<ModelPrice>().SingleAsync()).PerRequest);
        var results = await db.AuditEvents.Where(x => x.Action == Action).OrderBy(x => x.Id).Select(x => new { x.Result, x.OwnerId, x.DetailsJson }).ToListAsync();
        Assert.Equal([AuditEvent.Completed, "price_exists"], results.Select(x => x.Result));
        Assert.Equal(aliceId, results[1].OwnerId); Assert.Contains("\"modelId\":\"test-model\"", results[1].DetailsJson);
    }

    [Fact]
    public async Task EventHandlerFailureRollsBackTheProcedureWriteAndAuditsTheFailure()
    {
        var events = new PriceEvents { Fail = true };
        await using var factory = new AppFactory(administrators: ["alice"], services: events.Register);
        using var alice = await factory.SignedInAsync();
        var request = new PriceRequest("google", "test-model", "USD", "api", 1, 1, 2, 0, "completed", DateTimeOffset.UtcNow, "fixture");

        Assert.True((int)(await alice.PostAsJsonAsync("/api/v1/admin/billing/prices", request)).StatusCode >= 500);
        Assert.Single(events.Seen);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.Set<ModelPrice>().AnyAsync());
        // 價格隨交易回復；請求稽核另外記下這次失敗，不會留下成功紀錄。
        Assert.Equal("unexpected_error", (await db.AuditEvents.SingleAsync(x => x.Action == Action)).Result);
    }

    /// <summary>記錄事件，並確認處理器與 procedure 共用同一個交易（看得到未提交的價格）。</summary>
    private sealed class PriceEvents
    {
        public List<(Guid Id, bool Visible)> Seen { get; } = [];
        public bool Fail { get; init; }

        public void Register(IServiceCollection services) => services.AddScoped<IDomainEventHandler<ModelPriceCreated>>(scope => new Handler(this, scope.GetRequiredService<AppDbContext>()));

        private sealed class Handler(PriceEvents owner, AppDbContext db) : IDomainEventHandler<ModelPriceCreated>
        {
            public async Task HandleAsync(ModelPriceCreated e, CancellationToken ct)
            {
                owner.Seen.Add((e.PriceId, await db.Set<ModelPrice>().AnyAsync(x => x.Id == e.PriceId, ct)));
                if (owner.Fail) throw new InvalidOperationException("fixture handler failure");
            }
        }
    }
}
