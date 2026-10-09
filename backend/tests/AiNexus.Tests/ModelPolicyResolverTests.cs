using AiNexus.Features.Administration;
using Xunit;
using AiNexus.Features.Inference;

namespace AiNexus.Tests;

public sealed class ModelPolicyResolverTests
{
    [Fact]
    public void GroupModelsAccumulateAndPersonalModelsCanOnlyNarrowThem()
    {
        ModelPolicyRequest[] groups = [new(["b", "a"]), new(["b", "c"]), new([])];
        Assert.Equal(["a", "b", "c"], ModelPolicyResolver.Resolve(groups, new()).AllowedModelIds);
        Assert.Equal(["b"], ModelPolicyResolver.Resolve(groups, new(["b", "outside"])).AllowedModelIds);
        Assert.Empty(ModelPolicyResolver.Resolve(groups, new([])).AllowedModelIds!);
    }

    [Fact]
    public void AnUnrestrictedGroupGrantsAllModelsButStillHonorsPersonalRestrictions()
    {
        ModelPolicyRequest[] groups = [new(["a"]), new()];
        Assert.Null(ModelPolicyResolver.Resolve(groups, new()).AllowedModelIds);
        Assert.Equal(["b"], ModelPolicyResolver.Resolve(groups, new(["b"])).AllowedModelIds);
    }

    [Fact]
    public void MissingOrEmptyGroupGrantsCannotBeExpandedByAPersonalWhitelist()
    {
        foreach (ModelPolicyRequest[] groups in new ModelPolicyRequest[][] { [], [new([])] })
        {
            Assert.Empty(ModelPolicyResolver.Resolve(groups, new()).AllowedModelIds!);
            Assert.Empty(ModelPolicyResolver.Resolve(groups, new(["a"])).AllowedModelIds!);
        }
    }

    [Fact]
    public void EachModelUsesTheLargestBudgetAmongGroupsThatGrantIt()
    {
        ModelPolicyRequest[] groups = [
            new(["a", "b"], new Dictionary<string, long> { ["a"] = 100, ["b"] = 50, ["c"] = 9000 }),
            new(["a", "c"], new Dictionary<string, long> { ["a"] = 300, ["c"] = 500, ["b"] = 1 }),
            new([], new Dictionary<string, long> { ["a"] = 9000 })];
        var limits = ModelPolicyResolver.Resolve(groups, new()).DailyTokenLimits!;
        Assert.Equal(300, limits["a"]);
        Assert.Equal(50, limits["b"]);
        Assert.Equal(500, limits["c"]);
    }

    [Fact]
    public void AnUncappedGrantWinsIncludingAgainstAZeroBudget()
    {
        var capped = new ModelPolicyRequest(["a", "b"], new Dictionary<string, long> { ["a"] = 0, ["b"] = 100 });
        var limits = ModelPolicyResolver.Resolve([capped, new(["a"])], new()).DailyTokenLimits!;
        Assert.False(limits.ContainsKey("a"));
        Assert.Equal(100, limits["b"]);
        Assert.Empty(ModelPolicyResolver.Resolve([capped, new()], new()).DailyTokenLimits!);
        Assert.Equal(0, ModelPolicyResolver.Resolve([capped], new()).DailyTokenLimits!["a"]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(2000)]
    public void PersonalBudgetsCanDisableLowerOrRaiseInheritedLimits(long personalLimit)
    {
        ModelPolicyRequest[] groups = [new(["a", "b"], new Dictionary<string, long> { ["a"] = 1000, ["b"] = 500 })];
        var limits = ModelPolicyResolver.Resolve(groups, new(DailyTokenLimits: new Dictionary<string, long> { ["a"] = personalLimit })).DailyTokenLimits!;
        Assert.Equal(personalLimit, limits["a"]);
        Assert.Equal(500, limits["b"]);
        Assert.Equal(personalLimit, ModelPolicyResolver.Resolve([new()], new(DailyTokenLimits: new Dictionary<string, long> { ["a"] = personalLimit })).DailyTokenLimits!["a"]);
    }
}
