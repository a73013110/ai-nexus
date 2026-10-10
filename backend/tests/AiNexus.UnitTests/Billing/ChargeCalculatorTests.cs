using AiNexus.Features.Billing;

namespace AiNexus.UnitTests.Billing;

public sealed class ChargeCalculatorTests
{
    [Fact]
    public void TariffsCountCacheAndReasoningExactlyOnce()
    {
        var call = new ModelCharge { PriceId = Guid.NewGuid(), StartedAt = DateTimeOffset.UtcNow, InputTokens = 1_000_000,
            CachedInputTokens = 400_000, OutputTokens = 100_000, ReasoningTokens = 25_000,
            InputPerMillion = 1, CachedInputPerMillion = .2m, OutputPerMillion = 4, PerRequest = .5m, UsageComplete = true };
        ChargeCalculator.Finalize(call, "completed"); Assert.Equal(1.58m, call.Amount); Assert.Equal("metered", call.State);
        ChargeCalculator.Finalize(call, "completed"); Assert.Equal(1.58m, call.Amount);
    }

    [Theory]
    [InlineData(false, false, "completed", "not_started", 0d)]
    [InlineData(true, false, "completed", "unpriced", null)]
    [InlineData(true, true, "completed", "missing_usage", null)]
    [InlineData(true, true, "running", "pending", null)]
    public void UnknownUsageIsNeverInventedAsZero(bool started, bool priced, string outcome, string state, double? amount)
    {
        var call = new ModelCharge { StartedAt = started ? DateTimeOffset.UtcNow : null, PriceId = priced ? Guid.NewGuid() : null, InputPerMillion = 1, CachedInputPerMillion = 1 };
        ChargeCalculator.Finalize(call, outcome); Assert.Equal(state, call.State); Assert.Equal(amount is null ? null : (decimal?)amount.Value, call.Amount);
    }

    [Fact]
    public void FlatAndFreePricesWorkWithoutTokenMetadata()
    {
        var call = new ModelCharge { StartedAt = DateTimeOffset.UtcNow, PriceId = Guid.NewGuid(), PerRequest = 3, RequestCharge = "attempted" };
        ChargeCalculator.Finalize(call, "failed"); Assert.Equal(3m, call.Amount);
        call.RequestCharge = "completed"; ChargeCalculator.Finalize(call, "failed"); Assert.Equal(0m, call.Amount);
        call.PerRequest = 0; call.Kind = "free"; ChargeCalculator.Finalize(call, "completed"); Assert.Equal(0m, call.Amount); Assert.Equal("metered", call.State);
    }

    [Fact]
    public void PartialStreamCountsAreUnknownUntilFinalUsageArrives()
    {
        var call = new ModelCharge { PriceId = Guid.NewGuid(), StartedAt = DateTimeOffset.UtcNow, InputTokens = 100, OutputTokens = 20, InputPerMillion = 1, CachedInputPerMillion = 1 };
        ChargeCalculator.Finalize(call, "failed"); Assert.Equal("missing_usage", call.State); Assert.Null(call.Amount);
        call.UsageComplete = true; ChargeCalculator.Finalize(call, "failed"); Assert.Equal(.0001m, call.Amount);
    }
}
