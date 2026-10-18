using AiNexus.Features.Work.Repositories.Reviews;

namespace AiNexus.UnitTests.Repositories;

public sealed class RepositoryReviewBriefTests
{
    [Theory]
    [InlineData("{\"conclusion\":\"No issues\",\"changes\":[],\"findings\":[],\"limitation\":\"\"}", "conclusion.language")]
    [InlineData("{\"conclusion\":\"未完成", "json")]
    [InlineData("{\"changes\":[],\"findings\":[]}", "conclusion.missing")]
    [InlineData("{\"conclusion\":\"結論\",\"changes\":[],\"findings\":[{\"priority\":\"high\",\"location\":\"a.cs:1\",\"detail\":\"問題\"}]}", "findings.priority")]
    public void BriefRejectsEnglishIncompleteAndUnusableResponses(string json, string reason)
    {
        Assert.False(RepositoryReviewBrief.TryRender(json, "review", out _, out var actual));
        Assert.Equal(reason, actual);
    }

    [Fact]
    public void BriefRejectsTextFarBeyondItsLimitSoTheModelIsAskedAgain()
    {
        var json = $"{{\"conclusion\":\"{new string('中', 200)}\",\"changes\":[],\"findings\":[]}}";
        Assert.False(RepositoryReviewBrief.TryRender(json, "review", out _, out var reason));
        Assert.Equal("conclusion.length", reason);
    }

    [Fact]
    public void BriefNormalizesFencesExtraItemsAndMildOverruns()
    {
        var detail = "Statistics(int year) 未檢查 [Authorize]，" + new string('權', 70);
        var location = string.Join("、", Enumerable.Repeat("CoreWebApp/Areas/SET/Controllers/MentoryController.cs:120", 4));
        var json = "```json\n{\"conclusion\":\"新增統計頁\\n並加入篩選\",\"changes\":[\"一\",\"\",\"二\",\"三\",\"四\"]," +
            $"\"findings\":[{{\"priority\":\"p1\",\"location\":\"{location}\",\"detail\":\"{detail}\"}}],\"limitation\":null,}}\n```";

        Assert.True(RepositoryReviewBrief.TryRender(json, "review", out var markdown, out _));

        Assert.StartsWith("新增統計頁 並加入篩選", markdown);
        Assert.Contains("- 三", markdown); Assert.DoesNotContain("- 四", markdown);
        Assert.Contains("**P1**", markdown); Assert.Contains("…`：", markdown); Assert.Contains("權…", markdown);
        Assert.DoesNotContain("限制：", markdown);
    }

    [Fact]
    public void SummaryDropsFindingsInsteadOfFailingTheReport()
    {
        const string json = "{\"conclusion\":\"新增統計頁\",\"changes\":[\"新增圖表\"],\"findings\":[{\"priority\":\"P1\",\"location\":\"a.cs:1\",\"detail\":\"缺少授權\"}],\"limitation\":\"\"}";
        Assert.True(RepositoryReviewBrief.TryRender(json, "summary", out var markdown));
        Assert.DoesNotContain("優先確認", markdown);
    }
}
