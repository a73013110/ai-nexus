using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using AiNexus.Features.Knowledge.Embeddings;

namespace AiNexus.Features.Knowledge.Retrieval;

public sealed partial class QueryVectorCache(EmbeddingService embeddings, IOptions<KnowledgeOptions> options) : IDisposable
{
    private readonly IMemoryCache cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 512 });
    private readonly SemaphoreSlim[] gates = Enumerable.Range(0, 64).Select(_ => new SemaphoreSlim(1)).ToArray();
    public async Task<float[]> GetAsync(Guid actor, EmbeddingProfile profile, string query, CancellationToken ct)
    {
        var normalized = Whitespace().Replace(query.Normalize(NormalizationForm.FormKC).Trim(), " ");
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(normalized)); var key = (profile.Key, Convert.ToHexString(digest));
        if (options.Value.QueryCacheMinutes == 0) return (await embeddings.EmbedBatchAsync(actor, profile, [normalized], EmbeddingPurpose.Query, ct))[0];
        var gate = gates[digest[0] % gates.Length]; await gate.WaitAsync(ct);
        try
        {
            if (cache.TryGetValue<float[]>(key, out var vector)) return vector!;
            vector = (await embeddings.EmbedBatchAsync(actor, profile, [normalized], EmbeddingPurpose.Query, ct))[0];
            cache.Set(key, vector, new MemoryCacheEntryOptions { Size = 1, AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(options.Value.QueryCacheMinutes) });
            return vector;
        }
        finally { gate.Release(); }
    }
    public void Dispose() { cache.Dispose(); foreach (var gate in gates) gate.Dispose(); }
    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)] private static partial Regex Whitespace();
}
