using AiNexus.Features.Repositories;

namespace AiNexus.UnitTests.Repositories;

public sealed class RepositoryReviewBriefTests
{
    [Theory]
    [InlineData("{\"conclusion\":\"No issues\",\"changes\":[],\"findings\":[],\"limitation\":\"\"}")]
    [InlineData("{\"conclusion\":\"未發現明確缺陷\",\"changes\":[],\"findings\":[]}")]
    [InlineData("{\"conclusion\":\"結論\",\"changes\":[\"一\",\"二\",\"三\",\"四\"],\"findings\":[],\"limitation\":\"\"}")]
    public void BriefRejectsEnglishIncompleteAndExcessiveResponses(string json) =>
        Assert.False(RepositoryReviewBrief.TryRender(json, "review", out _));
}
