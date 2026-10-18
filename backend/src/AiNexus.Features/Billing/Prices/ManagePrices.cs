using AiNexus.Data.App;
using AiNexus.Data.App.Audit;
using AiNexus.Data.App.Billing;
using AiNexus.Features.Identity;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Billing.Prices;

public sealed record PriceRequest(string Provider, string ModelId, string Currency, string Kind,
    decimal InputPerMillion, decimal CachedInputPerMillion, decimal OutputPerMillion, decimal PerRequest,
    string RequestCharge, DateTimeOffset EffectiveAt, string Note);

internal sealed class PriceRequestValidator : RequestValidator<PriceRequest>
{
    public override string ProblemCode => "invalid_price";

    // 新價格只對 EffectiveAt 之後的呼叫生效，已記錄的費用不會被追溯改寫。
    public PriceRequestValidator(TimeProvider clock)
    {
        RuleFor(x => x.Provider).Must(x => x is "google" or "ollama" or "searxng" or "brave").WithErrorCode("unknown");
        RuleFor(x => x.ModelId).NotEmpty().MaximumLength(160)
            .Must(x => x.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or ':' or '.' or '/')).WithErrorCode("characters");
        RuleFor(x => x.Currency).Must(x => x.Length == 3 && x.All(char.IsAsciiLetterUpper)).WithErrorCode("iso_4217");
        RuleFor(x => x.Kind).Must(x => x is "api" or "internal" or "free").WithErrorCode("unknown");
        RuleFor(x => x.RequestCharge).Must(x => x is "completed" or "attempted").WithErrorCode("unknown");
        RuleFor(x => x.Note).MaximumLength(500);
        RuleFor(x => x.EffectiveAt).Must(x => x >= clock.GetUtcNow().AddMinutes(-5) && x <= clock.GetUtcNow().AddDays(366)).WithErrorCode("window");
        RuleForEach(x => new[] { x.InputPerMillion, x.CachedInputPerMillion, x.OutputPerMillion, x.PerRequest })
            .Must(x => x is >= 0 and <= 100000 && decimal.Round(x, 8) == x).WithErrorCode("range").OverridePropertyName("rates");
        RuleFor(x => x.CachedInputPerMillion).LessThanOrEqualTo(x => x.InputPerMillion);
        RuleFor(x => x).Must(x => x.Kind != "free" || x is { InputPerMillion: 0, CachedInputPerMillion: 0, OutputPerMillion: 0, PerRequest: 0 })
            .WithErrorCode("free_has_rates").OverridePropertyName("kind");
    }
}

/// <summary>管理用價格表：可計價的模型、既有價格版本，以及新增版本。</summary>
internal sealed class ManagePrices(AppDbContext db, BillingService billing)
{
    public static void Map(RouteGroupBuilder admin)
    {
        admin.MapGet("/targets", (ManagePrices handler) => TypedResults.Ok(handler.Targets()))
            .WithName("ListPriceTargets");
        admin.MapGet("/prices", async (ManagePrices handler, CancellationToken ct) => TypedResults.Ok(await handler.ListAsync(ct)))
            .WithName("ListModelPrices");
        admin.MapPost("/prices", (PriceRequest body, ICurrentUser user, ManagePrices handler, CancellationToken ct) =>
                handler.CreateAsync(user.Id, body, ct).ToHttpResultAsync())
            .WithName("CreateModelPrice");
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

    public async Task<Result<PriceDto>> CreateAsync(Guid actor, PriceRequest body, CancellationToken ct)
    {
        var exists = await db.Set<ModelPrice>().AnyAsync(
            x => x.Provider == body.Provider && x.ModelId == body.ModelId && x.EffectiveAt == body.EffectiveAt, ct);
        if (exists) return BillingErrors.PriceExists;
        var price = new ModelPrice
        {
            Provider = body.Provider,
            ModelId = body.ModelId,
            Currency = body.Currency,
            Kind = body.Kind,
            InputPerMillion = body.InputPerMillion,
            CachedInputPerMillion = body.CachedInputPerMillion,
            OutputPerMillion = body.OutputPerMillion,
            PerRequest = body.PerRequest,
            RequestCharge = body.RequestCharge,
            EffectiveAt = body.EffectiveAt.ToUniversalTime(),
            Note = body.Note.Trim(),
            CreatedBy = actor,
        };
        db.Add(price);
        db.AuditEvents.Add(new AuditEvent
        {
            OwnerId = actor,
            ResourceId = price.Id,
            Action = "billing.price.created",
            Result = "created",
        });
        await db.SaveChangesAsync(ct);
        return billing.Describe(price);
    }
}
