using AiNexus.Data.App;
using AiNexus.Data.App.Audit;
using AiNexus.Data.App.Billing;
using System.Text.Json;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Data.Sql;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Events;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.Data.SqlClient;

namespace AiNexus.Features.Admin.Billing.Prices;

public sealed record PriceRequest(string Provider, string ModelId, string Currency, string Kind,
    decimal InputPerMillion, decimal CachedInputPerMillion, decimal OutputPerMillion, decimal PerRequest,
    string RequestCharge, DateTimeOffset EffectiveAt, string Note);

internal sealed class PriceRequestValidator : RequestValidator<PriceRequest>
{
    public override string ProblemCode => "invalid_price";

    // 新價格只對 EffectiveAt 之後的呼叫生效，已記錄的費用不會被追溯改寫。
    public PriceRequestValidator(TimeProvider clock)
    {
        RuleFor(x => x.Provider).Must(x => x is "google" or "ollama" || BillingService.WebSearchProviders.Contains(x)).WithErrorCode("unknown");
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

/// <summary>新增價格版本後發出；與新增同一個交易。</summary>
public sealed record ModelPriceCreated(Guid PriceId, string Provider, string ModelId, DateTimeOffset EffectiveAt) : IDomainEvent;

/// <summary>
/// 新增一個價格版本，寫入在 <c>billing.usp_CreateModelPrice</c>。這是寫入 procedure 的範本（ADR 0011、0014）：
/// 程式開交易、procedure 寫資料與成功稽核、程式發 domain event 後提交；預期內的失敗在回復後由程式寫稽核。
/// </summary>
internal sealed class CreateModelPrice(AppDbContext db, ISqlDatabase<AppDbContext> sql, DomainEvents events, BillingService billing)
{
    private const string Action = "billing.price.created";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static void Map(RouteGroupBuilder admin) => admin
        .MapPost("/prices", (PriceRequest body, ICurrentUser user, CreateModelPrice handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, body, ct).ToHttpResultAsync())
        .WithName("CreateModelPrice");

    public async Task<Result<PriceDto>> HandleAsync(Guid actor, PriceRequest body, CancellationToken ct)
    {
        var effectiveAt = body.EffectiveAt.ToUniversalTime();
        try
        {
            return billing.Describe(await CreateAsync());
        }
        // 走到這裡時交易已因 await using 回復；在交易內寫的失敗稽核會跟著被回復，所以在交易外另外寫。
        catch (SqlException error) when (ProcedureError.Code(error) == BillingErrors.PriceExists.Code)
        {
            db.AuditEvents.Add(new AuditEvent
            {
                OwnerId = actor,
                Action = Action,
                Result = BillingErrors.PriceExists.Code,
                DetailsJson = JsonSerializer.Serialize(new { provider = body.Provider, modelId = body.ModelId, effectiveAt, failureCode = BillingErrors.PriceExists.Code }, Json),
            });
            await db.SaveChangesAsync(ct);
            return BillingErrors.PriceExists;
        }

        async Task<ModelPrice> CreateAsync()
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var price = (await sql.ExecuteProcedureAsync<ModelPrice>("billing.usp_CreateModelPrice", new
            {
                Id = Guid.NewGuid(),
                body.Provider,
                body.ModelId,
                body.Currency,
                body.Kind,
                body.InputPerMillion,
                body.CachedInputPerMillion,
                body.OutputPerMillion,
                body.PerRequest,
                body.RequestCharge,
                EffectiveAt = effectiveAt,
                Note = body.Note.Trim(),
                CreatedBy = actor,
            }, cancellationToken: ct)).Single();
            // procedure 不經過 SaveChanges，事件要明確發出；SaveChanges 派送事件，處理器的異動與 procedure 在同一個交易提交。
            events.Raise(new ModelPriceCreated(price.Id, price.Provider, price.ModelId, price.EffectiveAt));
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return price;
        }
    }
}
