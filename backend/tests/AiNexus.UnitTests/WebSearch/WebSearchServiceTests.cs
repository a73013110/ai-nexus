using System.Text.Json;
using AiNexus.Features.WebSearch;

namespace AiNexus.UnitTests.WebSearch;

public sealed class WebSearchServiceTests
{
    [Fact]
    public void SearchPromptIsBoundedAndLabelsOnlySavedSources()
    {
        var record = new WebSearchRecord { ResultsJson = JsonSerializer.Serialize(Enumerable.Range(1, 8).Select(n => new WebSourceDto(n, new string('文', 180), "https://example.org/" + new string('x', 900), new string('語', 600), DateTimeOffset.UtcNow))) };
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(WebSearchService.Prompt(record)) <= WebSearchService.ReservedTokens);
        Assert.Equal("https://example.org/a", WebSearchProvider.SafeUrl("https://example.org/a"));
    }
}
