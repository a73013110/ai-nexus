using System.Text;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Platform.Assistant;
using AiNexus.Features.Platform.Inference.ModelTasks;

namespace AiNexus.UnitTests.Assistant;

/// <summary>助理的提示詞：放進去多少依剩下的上下文決定，估算與 <see cref="ModelTaskService"/> 相同，放進去的一定不會被它拒絕。</summary>
public sealed class AssistantPromptTests
{
    private static AssistantToolResult View(int facts)
        => new("範圍", [.. Enumerable.Range(1, facts).Select(i => new AssistantFact($"項目{i}", new string('資', 40)))]);

    private static int Used(AssistantPromptPlan plan)
        => Encoding.UTF8.GetByteCount(plan.Prompt) + plan.History.Sum(x => Encoding.UTF8.GetByteCount(x.Content));

    [Fact]
    public void TheInstructionNamesThePageAndWritesEachModulesRulesOnce()
    {
        var instruction = AssistantPrompt.Instruction("測試頁", ["- 規則甲。", "- 規則甲。", "- 規則乙。"], tools: true);

        Assert.Contains("「測試頁」", instruction, StringComparison.Ordinal);
        Assert.Contains("呼叫工具", instruction, StringComparison.Ordinal);
        Assert.Equal(1, instruction.Split("- 規則甲。").Length - 1);
        Assert.Contains("- 規則乙。", instruction, StringComparison.Ordinal);
        Assert.DoesNotContain("呼叫工具", AssistantPrompt.Instruction(null, [], tools: false), StringComparison.Ordinal);
    }

    [Fact]
    public void RulesAndChecksAreMarkedAndChecksNameThePageThatFixesThem()
    {
        var result = new AssistantToolResult("範圍", [
            new("資料", "一般的事實"),
            AssistantFact.Rule("誰會列出", "持卡的人才列出"),
            AssistantFact.Check("王小明", "帳號已停用仍持卡", new(FeatureCatalog.AttendanceCards, "search=A1")),
        ]);

        var block = AssistantPrompt.Facts("查詢", result, 1, 30000);

        Assert.Contains("[1] 資料：一般的事實\n", block.Text, StringComparison.Ordinal);
        Assert.Contains("[2] 〈規則〉誰會列出：持卡的人才列出\n", block.Text, StringComparison.Ordinal);
        Assert.Contains($"[3] 〈檢查〉王小明：帳號已停用仍持卡（處理頁：{FeatureCatalog.Find(FeatureCatalog.AttendanceCards)!.Name}）\n", block.Text, StringComparison.Ordinal);
        // 系統指令說明兩種標記與資料範圍怎麼用，所有功能共用，不必每個工具各寫一次。
        var instruction = AssistantPrompt.Instruction(null, [], tools: true);
        Assert.Contains("〈規則〉", instruction, StringComparison.Ordinal);
        Assert.Contains("〈檢查〉", instruction, StringComparison.Ordinal);
        Assert.Contains(AssistantPrompt.ScopeLabel, instruction, StringComparison.Ordinal);
    }

    [Fact]
    public void ThePageFactsAreNumberedInOrderAndAllFitInALargeContext()
    {
        var plan = AssistantPrompt.Plan(new("哪一項最重要？"), View(3), "測試頁", 30000, tools: false);

        Assert.False(plan.Partial);
        Assert.Equal(3, plan.Facts.Count);
        Assert.StartsWith("<facts source=\"測試頁（目前畫面）\" scope=\"範圍\">\n[1] 項目1：", plan.Prompt, StringComparison.Ordinal);
        Assert.Contains("[3] 項目3：", plan.Prompt, StringComparison.Ordinal);
        Assert.EndsWith("問題：哪一項最重要？", plan.Prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void ASmallContextKeepsTheFirstFactsAndSaysTheRestAreMissing()
    {
        const int available = 3000;
        var plan = AssistantPrompt.Plan(new("還有誰？"), View(200), "測試頁", available, tools: false);

        Assert.True(plan.Partial);
        Assert.InRange(plan.Facts.Count, 1, 199);
        Assert.Equal("項目1", plan.Facts[0].Label);
        Assert.Contains("只列出一部分", plan.Prompt, StringComparison.Ordinal);
        Assert.InRange(Used(plan), 0, available);
    }

    [Fact]
    public void WithToolsThePageFactsLeaveHalfTheRoomForToolResults()
    {
        const int available = 6000;
        var alone = AssistantPrompt.Plan(new("還有誰？"), View(200), "測試頁", available, tools: false);
        var shared = AssistantPrompt.Plan(new("還有誰？"), View(200), "測試頁", available, tools: true);

        Assert.InRange(Used(shared), 0, available / 2 + 200);
        Assert.True(shared.Facts.Count < alone.Facts.Count);
    }

    [Fact]
    public void WithoutAPageOnlyTheQuestionIsAsked()
    {
        var plan = AssistantPrompt.Plan(new("出勤規則是什麼？"), null, null, 3000, tools: true);

        Assert.Equal("問題：出勤規則是什麼？", plan.Prompt);
        Assert.Empty(plan.Facts);
    }

    [Fact]
    public void ToolResultsContinueTheNumberingAndAreCutToTheirShare()
    {
        var block = AssistantPrompt.Facts("查詢", View(200), 4, 1500);

        Assert.StartsWith("<facts source=\"查詢\" scope=\"範圍\">\n[4] 項目1：", block.Text, StringComparison.Ordinal);
        Assert.True(block.Partial);
        Assert.InRange(Encoding.UTF8.GetByteCount(block.Text), 0, 1500);
    }

    [Fact]
    public void OlderTurnsAreDroppedFirstAndTheRestKeepTheirOrder()
    {
        var long_ = new string('答', 600);
        var history = new AssistantTurn[] { new("第一問", long_), new("第二問", long_), new("第三問", "短答") };

        var plan = AssistantPrompt.Plan(new("第四問", History: history), View(10), "測試頁", 7000, tools: false);

        Assert.Equal(["第二問", long_, "第三問", "短答"], plan.History.Select(x => x.Content));
        Assert.Equal(["user", "assistant", "user", "assistant"], plan.History.Select(x => x.Role));
        Assert.InRange(Used(plan), 0, 7000);
    }
}
