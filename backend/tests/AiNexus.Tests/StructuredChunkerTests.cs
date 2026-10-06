using AiNexus.BuildingBlocks;
using AiNexus.Modules.Knowledge;
using Microsoft.Extensions.Options;
using Xunit;

namespace AiNexus.Tests;

public sealed class StructuredChunkerTests
{
    private static ITextChunker Chunker(int target = 450, int max = 700, int min = 80, double overlap = .12) =>
        new StructuredChunker(Options.Create(new KnowledgeOptions { ChunkTargetTokens = target, ChunkMaxTokens = max, ChunkMinTokens = min, ChunkOverlapRatio = overlap }));
    [Fact]
    public void ChineseStructureAndCrossPageRangesAreRetained()
    {
        var chunks = Chunker().Chunk([new() { PageNumber = 1, Text = "第三章 採購規範\n第12條 核准流程\n承辦人應提出申請。" }, new() { PageNumber = 2, Text = "主管核准後始得採購。" }]);
        var chunk = Assert.Single(chunks); Assert.Equal(1, chunk.StartPage); Assert.Equal(2, chunk.EndPage);
        Assert.Contains("第三章", chunk.HeadingPath); Assert.Contains("第12條", chunk.Text);
    }
    [Fact]
    public void TableRowsRemainWholeAndLongRowsFailExplicitly()
    {
        var row = "| 核准人 | 主管與承辦 |";
        var chunks = Chunker(80, 100, 20).Chunk([new() { PageNumber = 1, Text = string.Join('\n', Enumerable.Repeat(row, 30)) }]);
        Assert.All(chunks, chunk => Assert.All(chunk.Text.Split('\n'), line => Assert.Equal(row, line)));
        Assert.Throws<ApiException>(() => Chunker(80, 100).Chunk([new() { PageNumber = 1, Text = "|" + new string('文', 120) + "|" }]));
    }
    [Fact]
    public void HardSplitNeverBreaksSurrogatePairsOrLosesText()
    {
        var text = string.Concat(Enumerable.Repeat("中文😀", 500));
        var chunks = Chunker(80, 100, 20, 0).Chunk([new() { PageNumber = 1, Text = text }]);
        Assert.Equal(text, string.Concat(chunks.Select(x => x.Text)));
        Assert.All(chunks, chunk => { Assert.True(chunk.TokenEstimate <= 100); Assert.False(char.IsLowSurrogate(chunk.Text[0])); Assert.False(char.IsHighSurrogate(chunk.Text[^1])); });
    }
    [Fact]
    public void ShortParagraphsMergeAndOverlapUsesWholeSentences()
    {
        var shortChunks = Chunker(100, 150, 30).Chunk([new() { PageNumber = 1, Text = "甲。\n\n乙。\n\n丙。" }]);
        Assert.Single(shortChunks);
        var sentences = Enumerable.Range(0, 12).Select(i => i + new string('文', 19) + "。").ToArray();
        var chunks = Chunker(80, 120, 10, .3).Chunk([new() { PageNumber = 1, Text = string.Concat(sentences) }]);
        Assert.True(chunks.Count > 1);
        Assert.All(chunks, x => Assert.EndsWith("。", x.Text));
        Assert.Contains(sentences[3], chunks[0].Text); Assert.StartsWith(sentences[3], chunks[1].Text);
    }
    [Theory]
    [InlineData("中文", 2)] [InlineData("abcdefgh", 2)] [InlineData("中文abcd", 3)] [InlineData("", 0)]
    public void TokensAreEstimatedByScript(string text, int expected) => Assert.Equal(expected, TokenEstimator.Estimate(text));
    [Fact]
    public void ChunkingAndInputChangesProduceDifferentProfiles()
    {
        var options = new KnowledgeOptions(); var profile = EmbeddingInput.Profile(options);
        options.ChunkTargetTokens++; Assert.NotEqual(profile, EmbeddingInput.Profile(options));
        var hash = EmbeddingInput.Hash(EmbeddingInput.Document("規範", "第三章", "本文"));
        Assert.NotEqual(hash, EmbeddingInput.Hash(EmbeddingInput.Document("規範", "第四章", "本文")));
    }
    [Fact]
    public void SettingsRejectUnsupportedDimensionsAndMissingRerankEndpoint()
    {
        Assert.True(KnowledgeOptions.Valid(new())); Assert.False(KnowledgeOptions.Valid(new() { Dimensions = 512 }));
        Assert.False(KnowledgeOptions.Valid(new() { Rerank = new() { Provider = "tei" } }));
        Assert.False(KnowledgeOptions.Valid(new() { ChunkOverlapRatio = double.NaN }));
    }
}
