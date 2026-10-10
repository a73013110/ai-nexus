using AiNexus.Features.Knowledge.Indexing;
using AiNexus.Features.Knowledge.Retrieval;

namespace AiNexus.UnitTests.Knowledge;

public sealed class RetrievalRankingTests
{
    private static KnowledgeHitDto Hit(Guid document, int ordinal, string text = "原文。", int page = 1) => new(document, "規範", page, text, 0, Guid.NewGuid(), page, ordinal);

    [Fact]
    public void RrfCombinesBothRanksAndHonorsWeights()
    {
        var doc = Guid.NewGuid(); var a = Hit(doc, 0); var b = Hit(doc, 1); var c = Hit(doc, 2);
        var result = RetrievalRanking.Fuse([a, b], [c, b], new() { FtsWeight = 2 });
        Assert.Equal(b.ChunkId, result[0].ChunkId); Assert.Equal(2, result[0].VectorRank); Assert.Equal(2, result[0].FtsRank);
        Assert.Equal(3.0 / 62, result[0].RrfScore!.Value, 10); Assert.Equal(3, result.Count);
    }

    [Fact]
    public void DiversityAndAdjacentMergeRemoveSentenceOverlapAndRetainPageRange()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        var result = RetrievalRanking.Context([Hit(a, 1, "重複句。第二頁。", 2), Hit(a, 0, "第一頁。重複句。"), Hit(a, 2), Hit(b, 0)], new() { MaxChunksPerDocument = 2 });
        Assert.Equal(2, result.Count); Assert.Equal("第一頁。重複句。第二頁。", result[0].Text); Assert.Equal(1, result[0].PageNumber); Assert.Equal(2, result[0].EndPage);
        Assert.Equal(b, result[1].DocumentId);
    }

    [Fact]
    public void ContextBudgetIncludesHeadersAndNeverCutsSurrogates()
    {
        var result = RetrievalRanking.Context([Hit(Guid.NewGuid(), 0, new string('文', 300) + "😀")], new() { ContextTokens = 100 });
        Assert.True(TokenEstimator.Estimate(result[0].Text + result[0].Title) + 32 <= 100);
        Assert.False(char.IsHighSurrogate(result[0].Text[^1]));
    }
}
