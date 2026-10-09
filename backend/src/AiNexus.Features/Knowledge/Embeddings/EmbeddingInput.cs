using System.Security.Cryptography;
using System.Text;
using AiNexus.Features.Knowledge.Indexing;

namespace AiNexus.Features.Knowledge.Embeddings;

public static class EmbeddingInput
{
    public static string Format(KnowledgeOptions options, string text, bool document) =>
        !document && options.InputFormat == "qwen-query" ? $"Instruct: {options.QueryInstruction}\nQuery: {text}" : text;
    public static string Profile(KnowledgeOptions options)
    {
        var basis = options.EmbeddingProvider + ":" + options.EmbeddingModel + ":" + options.Dimensions;
        var rules = FormattableString.Invariant($"{StructuredChunker.Version}:{options.ChunkTargetTokens}:{options.ChunkMaxTokens}:{options.ChunkMinTokens}:{options.ChunkOverlapRatio}");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(options.InputFormat + "\n" + options.QueryInstruction + "\n" + options.Revision + "\n" + rules)))[..16];
        return basis + ":" + hash;
    }
    public static string Document(string title, string heading, string text) => $"{title}{(heading.Length == 0 ? "" : " › " + heading)}\n{text}";
    public static byte[] Hash(string input) => SHA256.HashData(Encoding.UTF8.GetBytes(input));
    public static string Format(EmbeddingProfile profile, string text, EmbeddingPurpose purpose) =>
        purpose == EmbeddingPurpose.Query && profile.InputFormat == "qwen-query" ? $"Instruct: {profile.QueryInstruction}\nQuery: {text}" : text;
}
