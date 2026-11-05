using System.Text;
using AiNexus.Features.Platform.Assistant;
using AiNexus.Features.Platform.Inference.ModelTasks;

namespace AiNexus.UnitTests.Assistant;

/// <summary>頁內助理的提示詞：放進去多少依模型的上下文決定，估算與 <see cref="ModelTaskService"/> 相同，放進去的一定不會被它拒絕。</summary>
public sealed class AssistantPromptTests
{
    private static AssistantGrounding Grounding(int facts)
        => new("測試頁", "範圍", "- 測試頁的規則。", [.. Enumerable.Range(1, facts).Select(i => new AssistantFact($"項目{i}", new string('資', 40)))]);

    private static int Estimate(AssistantPromptPlan plan, int output)
        => Encoding.UTF8.GetByteCount(plan.Prompt + plan.Instruction) + plan.History.Sum(x => Encoding.UTF8.GetByteCount(x.Content))
            + output + ModelTaskService.FramingTokenReserve;

    [Fact]
    public void FactsAreNumberedInPageOrderAndAllFitInALargeContext()
    {
        var plan = AssistantPrompt.Plan(Grounding(3), new("哪一項最重要？"), 32768, 1536);

        Assert.False(plan.Partial);
        Assert.Equal(3, plan.Facts.Count);
        Assert.StartsWith("<facts>\n[1] 項目1：", plan.Prompt, StringComparison.Ordinal);
        Assert.Contains("[3] 項目3：", plan.Prompt, StringComparison.Ordinal);
        Assert.EndsWith("問題：哪一項最重要？", plan.Prompt, StringComparison.Ordinal);
        Assert.Contains("「測試頁」", plan.Instruction, StringComparison.Ordinal);
        Assert.Contains("- 測試頁的規則。", plan.Instruction, StringComparison.Ordinal);
    }

    [Fact]
    public void ASmallContextKeepsTheFirstFactsAndSaysTheRestAreMissing()
    {
        const int context = 4096, output = 512;
        var plan = AssistantPrompt.Plan(Grounding(200), new("還有誰？"), context, output);

        Assert.True(plan.Partial);
        Assert.InRange(plan.Facts.Count, 1, 199);
        Assert.Equal("項目1", plan.Facts[0].Label);
        Assert.Contains("只列出一部分", plan.Prompt, StringComparison.Ordinal);
        Assert.InRange(Estimate(plan, output), 0, context);
    }

    [Fact]
    public void OlderTurnsAreDroppedFirstAndTheRestKeepTheirOrder()
    {
        var long_ = new string('答', 600);
        var history = new AssistantTurn[] { new("第一問", long_), new("第二問", long_), new("第三問", "短答") };

        var plan = AssistantPrompt.Plan(Grounding(10), new("第四問", History: history), 8192, 512);

        Assert.Equal(["第二問", long_, "第三問", "短答"], plan.History.Select(x => x.Content));
        Assert.Equal(["user", "assistant", "user", "assistant"], plan.History.Select(x => x.Role));
        Assert.InRange(Estimate(plan, 512), 0, 8192);
    }
}
