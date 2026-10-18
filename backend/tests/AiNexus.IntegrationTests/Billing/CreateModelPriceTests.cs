using AiNexus.Data.App;
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

public sealed class CreateModelPriceTests
{
    private const string Procedure = "billing.usp_CreateModelPrice";

    [Fact]
    public async Task ProcedureWritesPriceAndSuccessAuditAndTheEventCommitsInTheSameTransaction()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var audits = new ProcedureAudits(); var events = new PriceEvents();
        await using var factory = new AppFactory(administrators: ["alice"], clock: clock, services: services => { audits.Register(services); events.Register(services); });
        using var alice = await factory.SignedInAsync();
        var aliceId = (await alice.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        var request = new PriceRequest("google", "test-model", "USD", "api", 1, .5m, 2, 0, "completed", clock.GetUtcNow().AddDays(1), "  fixture  ");

        var response = await alice.PostAsJsonAsync("/api/v1/admin/billing/prices", request); response.EnsureSuccessStatusCode();
        var price = (await response.Content.ReadFromJsonAsync<PriceDto>())!;
        Assert.Equal(("fixture", "測試模型", request.EffectiveAt), (price.Note, price.ModelDisplayName, price.EffectiveAt));
        var audit = Assert.Single(await audits.AssertWrittenAsync(factory, Procedure, aliceId, clock.GetUtcNow(), "administration"));
        Assert.Equal(("billing.price.created", "created", price.Id), (audit.Action, audit.Result, (Guid?)audit.ResourceId));
        using var details = JsonDocument.Parse(audit.DetailsJson!);
        Assert.Equal("test-model", details.RootElement.GetProperty("modelId").GetString());
        // 事件處理器在提交前執行，看得到 procedure 尚未提交的資料列。
        Assert.Equal([(price.Id, true)], events.Seen);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.Set<ModelPrice>().SingleAsync();
        Assert.Equal((aliceId, clock.GetUtcNow()), (row.CreatedBy, row.CreatedAt));
    }

    [Fact]
    public async Task DuplicateIsRejectedByTheProcedureAndAuditedByCodeAfterRollback()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var audits = new ProcedureAudits();
        await using var factory = new AppFactory(administrators: ["alice"], clock: clock, services: audits.Register);
        using var alice = await factory.SignedInAsync();
        var aliceId = (await alice.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        var request = new PriceRequest("google", "test-model", "USD", "api", 1, 1, 2, 0, "completed", clock.GetUtcNow(), "fixture");
        (await alice.PostAsJsonAsync("/api/v1/admin/billing/prices", request)).EnsureSuccessStatusCode();

        var duplicate = await alice.PostAsJsonAsync("/api/v1/admin/billing/prices", request with { PerRequest = 9 });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Contains("price_exists", await duplicate.Content.ReadAsStringAsync());
        await audits.AssertWrittenAsync(factory, Procedure, aliceId, clock.GetUtcNow(), "administration");
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(0, (await db.Set<ModelPrice>().SingleAsync()).PerRequest);
        var results = await db.AuditEvents.Where(x => x.Action == "billing.price.created").OrderBy(x => x.Id).Select(x => new { x.Result, x.OwnerId, x.DetailsJson }).ToListAsync();
        Assert.Equal(["created", "price_exists"], results.Select(x => x.Result));
        Assert.Equal(aliceId, results[1].OwnerId); Assert.Contains("\"failureCode\":\"price_exists\"", results[1].DetailsJson);
    }

    [Fact]
    public async Task EventHandlerFailureRollsBackTheProcedureWriteAndItsAudit()
    {
        var events = new PriceEvents { Fail = true };
        await using var factory = new AppFactory(administrators: ["alice"], services: events.Register);
        using var alice = await factory.SignedInAsync();
        var request = new PriceRequest("google", "test-model", "USD", "api", 1, 1, 2, 0, "completed", DateTimeOffset.UtcNow, "fixture");

        Assert.True((int)(await alice.PostAsJsonAsync("/api/v1/admin/billing/prices", request)).StatusCode >= 500);
        Assert.Single(events.Seen);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.Set<ModelPrice>().AnyAsync());
        Assert.False(await db.AuditEvents.AnyAsync(x => x.Action == "billing.price.created"));
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
