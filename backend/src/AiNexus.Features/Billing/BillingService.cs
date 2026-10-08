using AiNexus.Platform.Errors;
using AiNexus.Features.Persistence;
using AiNexus.Features.Inference;
using AiNexus.Features.Operations;
using AiNexus.Features.Knowledge;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Billing;

public sealed class BillingService(NexusDbContext db, ModelPresentation presentation, IOptions<InferenceOptions> inference, IOptions<KnowledgeOptions> knowledge)
{
    public IReadOnlyList<PriceTargetDto> Targets() => inference.Value.Models
        .Select(x => Target(x.Provider, x.NativeId))
        .Append(Target(knowledge.Value.EmbeddingProvider, knowledge.Value.EmbeddingModel))
        .Concat(new[] { "searxng", "brave" }.Select(x => Target(x, "web-search")))
        .DistinctBy(x => (x.Provider, x.ModelId)).ToArray();
    private PriceTargetDto Target(string provider, string model) => new(provider, model, presentation.DisplayName(model, administrator: true, provider: provider)!);
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
    public PriceDto Describe(ModelPrice p) => new(p.Id, p.Provider, p.ModelId, p.Currency, p.Kind,
        p.InputPerMillion, p.CachedInputPerMillion, p.OutputPerMillion, p.PerRequest, p.RequestCharge, p.EffectiveAt, p.Note,
        presentation.DisplayName(p.ModelId, administrator: true, provider: p.Provider));
}
