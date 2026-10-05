using System.Security.Cryptography;
using System.Text;

namespace AiNexus.Modules.Knowledge;

public static class EmbeddingInput
{
    public static string Format(KnowledgeOptions options, string text, bool document) =>
        !document && options.InputFormat == "qwen-query" ? $"Instruct: {options.QueryInstruction}\nQuery: {text}" : text;
    public static string Profile(KnowledgeOptions options)
    {
        var basis = options.EmbeddingProvider + ":" + options.EmbeddingModel + ":" + options.Dimensions;
        if (options.InputFormat == "plain" && options.Revision.Length == 0) return basis;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(options.InputFormat + "\n" + options.QueryInstruction + "\n" + options.Revision)))[..16];
        return basis + ":" + hash;
    }
}
