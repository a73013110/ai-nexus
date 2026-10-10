using System.Text.RegularExpressions;
using AiNexus.Platform.Errors;
using Microsoft.Extensions.Options;
using System.Data;
using AiNexus.Features.Knowledge.Documents;

namespace AiNexus.Features.Knowledge.Indexing;

public sealed record StructuredChunk(int StartPage, int EndPage, string HeadingPath, string Text, int TokenEstimate);

public sealed partial class StructuredChunker(IOptions<KnowledgeOptions> options) : ITextChunker
{
    public const int Version = 1;
    private sealed record Block(int Page, string Heading, string Text, bool Boundary, bool Table);
    public Result<IReadOnlyList<StructuredChunk>> Chunk(IReadOnlyList<DocumentPage> pages, ChunkerSnapshot? configuration = null)
    {
        var settings = configuration?.Options() ?? options.Value; var blocks = new List<Block>(); var headings = new SortedDictionary<int, string>();
        foreach (var page in pages.OrderBy(x => x.PageNumber))
        {
            var blank = true;
            foreach (var raw in page.Text.Replace("\r\n", "\n").Split('\n'))
            {
                var line = raw.Trim(); if (line.Length == 0) { blank = true; continue; }
                var level = HeadingLevel(line);
                if (level > 0)
                {
                    foreach (var key in headings.Keys.Where(x => x >= level).ToArray()) headings.Remove(key);
                    headings[level] = line.TrimStart('#', ' ');
                }
                var path = string.Join(" › ", headings.Values);
                if (path.Length > 400) path = SafePrefix(path, 400);
                var table = line.Contains('|') || line.Contains('\t');
                if (table && (TokenEstimator.Estimate(line) > settings.Indexing.ChunkMaxTokens || line.Length > 4000))
                    return KnowledgeErrors.TableRowTooLong;
                var parts = table ? [line] : Sentences().Matches(line).Select(x => x.Value.Trim()).Where(x => x.Length > 0).ToArray();
                foreach (var part in parts)
                {
                    var rest = part;
                    while (rest.Length > 0)
                    {
                        var piece = TokenEstimator.Truncate(rest, settings.Indexing.ChunkMaxTokens);
                        piece = SafePrefix(piece, 4000);
                        blocks.Add(new(page.PageNumber, path, piece, level > 0 || blank, table));
                        rest = rest[piece.Length..].TrimStart(); blank = false; level = 0;
                    }
                }
            }
        }
        var result = new List<StructuredChunk>(); var pending = new List<Block>();
        string Text() => string.Join('\n', pending.Select(x => x.Text));
        void Flush(bool overlap)
        {
            if (pending.Count == 0) return;
            var text = Text(); result.Add(new(pending[0].Page, pending[^1].Page, pending[^1].Heading, text, TokenEstimator.Estimate(text)));
            var tail = new List<Block>(); var budget = (int)(settings.Indexing.ChunkTargetTokens * settings.Indexing.ChunkOverlapRatio);
            if (overlap)
                foreach (var block in pending.AsEnumerable().Reverse())
                {
                    if (block.Table || !SentenceEnd().IsMatch(block.Text) || TokenEstimator.Estimate(string.Join('\n', tail.Select(x => x.Text).Prepend(block.Text))) > budget) break;
                    tail.Insert(0, block);
                }
            pending = tail;
        }
        foreach (var block in blocks)
        {
            var length = TokenEstimator.Estimate(Text());
            var headingChanged = pending.Count > 0 && pending[0].Heading != block.Heading;
            if (pending.Count > 0 && (headingChanged && length >= settings.Indexing.ChunkMinTokens || length >= settings.Indexing.ChunkTargetTokens && block.Boundary
                || TokenEstimator.Estimate(Text() + "\n" + block.Text) > settings.Indexing.ChunkMaxTokens || Text().Length + block.Text.Length + 1 > 4000))
                Flush(!headingChanged);
            // Drop overlap if it would force a hard split of the next sentence or table row.
            if (pending.Count > 0 && TokenEstimator.Estimate(Text() + "\n" + block.Text) > settings.Indexing.ChunkMaxTokens) pending.Clear();
            pending.Add(block);
            if (TokenEstimator.Estimate(Text()) >= settings.Indexing.ChunkTargetTokens && !block.Table) Flush(true);
        }
        if (pending.Count > 0)
        {
            var text = Text();
            if (result.Count > 0 && result[^1].Text.EndsWith(text, StringComparison.Ordinal)) pending.Clear();
            else if (result.Count > 0 && TokenEstimator.Estimate(text) < settings.Indexing.ChunkMinTokens
                && TokenEstimator.Estimate(result[^1].Text + "\n" + text) <= settings.Indexing.ChunkMaxTokens && result[^1].Text.Length + text.Length + 1 <= 4000)
            {
                var last = result[^1]; var combined = last.Text + "\n" + text;
                result[^1] = last with { EndPage = pending[^1].Page, Text = combined, TokenEstimate = TokenEstimator.Estimate(combined) }; pending.Clear();
            }
            Flush(false);
        }
        return Result<IReadOnlyList<StructuredChunk>>.Ok(result);
    }
    private static string SafePrefix(string text, int length)
    {
        if (text.Length <= length) return text;
        if (char.IsHighSurrogate(text[length - 1])) length--;
        return text[..length];
    }
    private static int HeadingLevel(string text)
    {
        var markdown = MarkdownHeading().Match(text); if (markdown.Success) return markdown.Groups[1].Length;
        var chinese = ChineseHeading().Match(text);
        if (chinese.Success) return chinese.Groups[1].Value switch { "章" => 1, "節" => 2, "條" => 3, "款" => 4, _ => 5 };
        if (NumberHeading().IsMatch(text)) return text.StartsWith('（') || text.StartsWith('(') ? 5 : 4;
        return 0;
    }
    [GeneratedRegex(@"^(#{1,6})\s+", RegexOptions.CultureInvariant)] private static partial Regex MarkdownHeading();
    [GeneratedRegex(@"^第[一二三四五六七八九十百千零〇兩\d]+([章節條款項])", RegexOptions.CultureInvariant)] private static partial Regex ChineseHeading();
    [GeneratedRegex(@"^(?:[一二三四五六七八九十百]+、|（[一二三四五六七八九十百]+）|\d+\.\s|\(\d+\))", RegexOptions.CultureInvariant)] private static partial Regex NumberHeading();
    [GeneratedRegex(@"[^。！？；.!?]+[。！？；.!?]*|[。！？；.!?]+", RegexOptions.CultureInvariant)] private static partial Regex Sentences();
    [GeneratedRegex(@"[。！？；.!?]$", RegexOptions.CultureInvariant)] private static partial Regex SentenceEnd();
}

public sealed record ChunkerSnapshot(int Version, int TargetTokens, int MaxTokens, int MinTokens, double OverlapRatio)
{
    public static ChunkerSnapshot Capture(KnowledgeOptions x) => new(StructuredChunker.Version, x.Indexing.ChunkTargetTokens, x.Indexing.ChunkMaxTokens, x.Indexing.ChunkMinTokens, x.Indexing.ChunkOverlapRatio);
    public KnowledgeOptions Options() => new() { Indexing = new() { ChunkTargetTokens = TargetTokens, ChunkMaxTokens = MaxTokens, ChunkMinTokens = MinTokens, ChunkOverlapRatio = OverlapRatio } };
}
