using AiNexus.Data.App;
using AiNexus.Data.App.Billing;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Billing.Prices;

/// <summary>管理用價格表：可計價的模型與既有價格版本；新增版本見 <see cref="CreateModelPrice"/>。</summary>
internal sealed class ManagePrices(AppDbContext db, BillingService billing)
{
    public static void Map(RouteGroupBuilder admin)
    {
        admin.MapGet("/targets", (ManagePrices handler) => TypedResults.Ok(handler.Targets()))
            .WithName("ListPriceTargets");
        admin.MapGet("/prices", async (ManagePrices handler, CancellationToken ct) => TypedResults.Ok(await handler.ListAsync(ct)))
            .WithName("ListModelPrices");
    }

    public IReadOnlyList<PriceTargetDto> Targets() => billing.Targets();

    public async Task<IReadOnlyList<PriceDto>> ListAsync(CancellationToken ct)
    {
        var prices = await db.Set<ModelPrice>().AsNoTracking()
            .OrderByDescending(x => x.EffectiveAt)
            .Take(500)
            .ToListAsync(ct);
        return prices.Select(billing.Describe).ToArray();
    }
}
