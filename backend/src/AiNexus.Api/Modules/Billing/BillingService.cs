using AiNexus.BuildingBlocks;
using AiNexus.Modules.Inference;
using AiNexus.Modules.Operations;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Modules.Billing;

public sealed class BillingService(NexusDbContext db)
{
    public async Task<ModelCharge> ReserveAsync(Guid callId, Guid owner, Guid? conversation, string provider,
        string model, string operation, DateTimeOffset created, CancellationToken ct)
    {
        var price = await db.Set<ModelPrice>().AsNoTracking().Where(x => x.Provider == provider && x.ModelId == model && x.EffectiveAt <= created)
            .OrderByDescending(x => x.EffectiveAt).FirstOrDefaultAsync(ct);
        var call = new ModelCharge { Id = callId, OwnerId = owner, ConversationId = conversation, Provider = provider,
            ModelId = model, Operation = operation, CreatedAt = created, PriceId = price?.Id, Currency = price?.Currency ?? "",
            Kind = price?.Kind ?? "unpriced", InputPerMillion = price?.InputPerMillion ?? 0,
            CachedInputPerMillion = price?.CachedInputPerMillion ?? 0, OutputPerMillion = price?.OutputPerMillion ?? 0,
            PerRequest = price?.PerRequest ?? 0, RequestCharge = price?.RequestCharge ?? "completed" };
        db.Add(call); return call;
    }
    public async Task StartAsync(Guid id, CancellationToken ct)
    {
        var charge = await db.Set<ModelCharge>().FindAsync([id], ct);
        if (charge is null) return; // Calls made before the billing migration have no invented price.
        charge.StartedAt ??= DateTimeOffset.UtcNow; charge.Outcome = "running";
    }
    public async Task MeterAsync(Guid id, long? input, long? output, long? cached, long? reasoning, CancellationToken ct, bool complete = false)
    {
        var charge = await db.Set<ModelCharge>().FindAsync([id], ct);
        if (charge is null) return;
        charge.InputTokens = input ?? charge.InputTokens; charge.OutputTokens = output ?? charge.OutputTokens;
        charge.CachedInputTokens = cached ?? charge.CachedInputTokens; charge.ReasoningTokens = reasoning ?? charge.ReasoningTokens;
        charge.UsageComplete |= complete;
    }
    public async Task FinishAsync(Guid id, string outcome, CancellationToken ct)
    {
        var charge = await db.Set<ModelCharge>().FindAsync([id], ct);
        if (charge is null) return;
        charge.FinishedAt ??= DateTimeOffset.UtcNow; ChargeCalculator.Finalize(charge, outcome);
    }
    public async Task<IReadOnlyList<PriceDto>> PricesAsync(CancellationToken ct) =>
        (await db.Set<ModelPrice>().AsNoTracking().OrderByDescending(x => x.EffectiveAt).Take(500).ToListAsync(ct)).Select(Describe).ToArray();
    public async Task<PriceDto> AddPriceAsync(Guid actor, PriceRequest body, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        if (body.Provider is not ("google" or "ollama" or "searxng" or "brave")
            || string.IsNullOrEmpty(body.ModelId) || body.ModelId.Length > 160 || body.ModelId.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_' and not ':' and not '.' and not '/')
            || body.Currency is null || body.Currency.Length != 3 || !body.Currency.All(char.IsAsciiLetterUpper) || body.Kind is not ("api" or "internal" or "free")
            || body.RequestCharge is not ("completed" or "attempted") || body.Note is null || body.Note.Length > 500
            || body.EffectiveAt < now.AddMinutes(-5) || body.EffectiveAt > now.AddDays(366)
            || new[] { body.InputPerMillion, body.CachedInputPerMillion, body.OutputPerMillion, body.PerRequest }.Any(x => x < 0 || x > 100000 || decimal.Round(x, 8) != x)
            || body.CachedInputPerMillion > body.InputPerMillion
            || (body.Kind == "free" && new[] { body.InputPerMillion, body.CachedInputPerMillion, body.OutputPerMillion, body.PerRequest }.Any(x => x != 0)))
            throw new ApiException(400, "invalid_price", "請檢查模型與工具價格、幣別、生效時間與計費方式。新價格只能由現在起生效。");
        if (await db.Set<ModelPrice>().AnyAsync(x => x.Provider == body.Provider && x.ModelId == body.ModelId && x.EffectiveAt == body.EffectiveAt, ct))
            throw new ApiException(409, "price_exists", "此生效時間已有價格版本，請選擇新的時間。");
        var price = new ModelPrice { Provider = body.Provider, ModelId = body.ModelId, Currency = body.Currency, Kind = body.Kind,
            InputPerMillion = body.InputPerMillion, CachedInputPerMillion = body.CachedInputPerMillion, OutputPerMillion = body.OutputPerMillion,
            PerRequest = body.PerRequest, RequestCharge = body.RequestCharge, EffectiveAt = body.EffectiveAt.ToUniversalTime(), Note = body.Note.Trim(), CreatedBy = actor };
        db.Add(price); db.AuditEvents.Add(new AuditEvent { OwnerId = actor, ResourceId = price.Id, Action = "billing.price.created", Result = body.Provider + ":" + body.ModelId });
        await db.SaveChangesAsync(ct); return Describe(price);
    }
    private static PriceDto Describe(ModelPrice p) => new(p.Id, p.Provider, p.ModelId, p.Currency, p.Kind,
        p.InputPerMillion, p.CachedInputPerMillion, p.OutputPerMillion, p.PerRequest, p.RequestCharge, p.EffectiveAt, p.Note);
}
