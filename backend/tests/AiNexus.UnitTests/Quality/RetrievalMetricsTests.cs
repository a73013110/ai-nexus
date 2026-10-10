using AiNexus.Features.Knowledge.Retrieval;
using AiNexus.Features.Quality.RetrievalEvaluations;

namespace AiNexus.UnitTests.Quality;

public sealed class RetrievalMetricsTests
{
    private static KnowledgeHitDto Hit(Guid document, int start = 1, int end = 1) => new(document, "來源原文不應存入報告", start, "來源原文不應存入報告", 1, Guid.NewGuid(), end);

    [Fact]
    public void MetricsUseGradedDocumentRelevanceAndPageRangesWithoutDoubleCredit()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var c = Guid.NewGuid();
        var test = new RetrievalEvaluationCase("題一", "查詢", [new(a, [3], 3), new(b, [1], 1)]);
        var result = RetrievalMetrics.Measure(test, [Hit(c), Hit(a, 2, 4), Hit(a, 3, 3), Hit(b)], 4);
        Assert.Equal(1, result.Recall); Assert.Equal(.5, result.ReciprocalRank);
        Assert.Equal((7 / Math.Log2(3) + 1 / Math.Log2(5)) / (7 + 1 / Math.Log2(3)), result.Ndcg!.Value, 10);
        var wrongPage = RetrievalMetrics.Measure(test, [Hit(a), Hit(b)], 1);
        Assert.Equal(0, wrongPage.Recall); Assert.Equal(0, wrongPage.ReciprocalRank); Assert.Equal(0, wrongPage.Ndcg);
    }

    [Fact]
    public void NoAnswerAndLatencyPercentilesHaveExplicitDefinitions()
    {
        var test = new RetrievalEvaluationCase("無答案", "查詢", [], true);
        var refused = RetrievalMetrics.Measure(test, [], 6); Assert.True(refused.Refused); Assert.Null(refused.Recall);
        Assert.False(RetrievalMetrics.Measure(test, [Hit(Guid.NewGuid())], 6).Refused);
        var latency = RetrievalMetrics.Latency([10, 20, 30, 40]); Assert.Equal(25, latency.P50); Assert.Equal(38.5, latency.P95);
        Assert.Equal(new RetrievalLatencyDto(0, 0), RetrievalMetrics.Latency([]));
    }

    [Fact]
    public void IncompleteReportsAreNotMarkedComparable()
    {
        var result = new RetrievalMetricDto("一", "vector", "vector", null, 1, 1, 1, null, 0, 0, 1, 0, 1);
        Assert.False(RetrievalMetrics.Summarize([result], 2).Single(x => x.Mode == "vector").Comparable);
        Assert.True(RetrievalMetrics.Summarize([result], 1).Single(x => x.Mode == "vector").Comparable);
    }
}
