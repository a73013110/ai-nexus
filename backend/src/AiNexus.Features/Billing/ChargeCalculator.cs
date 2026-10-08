namespace AiNexus.Features.Billing;

public static class ChargeCalculator
{
    public static void Finalize(ModelCharge call, string outcome)
    {
        call.Outcome = outcome;
        if (outcome is "queued" or "running") { call.State = "pending"; call.Amount = null; return; }
        if (call.StartedAt is null) { call.State = "not_started"; call.Amount = 0; return; }
        if (call.PriceId is null) { call.State = "unpriced"; call.Amount = null; return; }
        var needsInput = call.InputPerMillion != 0 || call.CachedInputPerMillion != 0;
        var needsCache = call.InputPerMillion != call.CachedInputPerMillion;
        if (((needsInput || call.OutputPerMillion != 0) && !call.UsageComplete) || (needsInput && call.InputTokens is null) || (needsCache && call.CachedInputTokens is null)
            || (call.OutputPerMillion != 0 && call.OutputTokens is null)
            || call.InputTokens < 0 || call.OutputTokens < 0 || call.CachedInputTokens < 0
            || call.ReasoningTokens < 0 || (call.ReasoningTokens ?? 0) > (call.OutputTokens ?? 0)
            || (call.CachedInputTokens ?? 0) > (call.InputTokens ?? 0))
        { call.State = "missing_usage"; call.Amount = null; return; }
        var cached = call.CachedInputTokens ?? 0;
        var input = (call.InputTokens ?? 0) - cached;
        var flat = call.RequestCharge == "attempted" || outcome == "completed" ? call.PerRequest : 0;
        call.Amount = decimal.Round((input * call.InputPerMillion + cached * call.CachedInputPerMillion
            + (call.OutputTokens ?? 0) * call.OutputPerMillion) / 1_000_000m + flat, 8, MidpointRounding.AwayFromZero);
        call.State = "metered";
    }

    public static ChargeDto Describe(ModelCharge c) => new(c.State, c.Kind, c.Currency, c.Amount,
        c.InputTokens, c.CachedInputTokens, c.OutputTokens, c.ReasoningTokens);
}
