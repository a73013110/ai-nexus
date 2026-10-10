using System.ComponentModel.DataAnnotations;
using AiNexus.Platform.Configuration;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Knowledge;

public sealed class KnowledgeOptions
{
    public const string Section = "Knowledge";

    [ValidateObjectMembers] public EmbeddingOptions Embedding { get; set; } = new();
    [ValidateObjectMembers] public IndexingOptions Indexing { get; set; } = new();
    [ValidateObjectMembers] public RetrievalOptions Retrieval { get; set; } = new();
    [ValidateObjectMembers] public RerankOptions Rerank { get; set; } = new();
    [ValidateObjectMembers] public QueryRewriteOptions QueryRewrite { get; set; } = new();
}

public sealed class EmbeddingOptions : IValidatableObject
{
    [AllowedValues("google", "ollama", "none")] public string Provider { get; set; } = "ollama";
    [Required, MaxLength(160), RegularExpression("^[A-Za-z0-9._:-]+$")] public string Model { get; set; } = "bge-m3";
    public int Dimensions { get; set; } = 1024;
    [AllowedValues("plain", "qwen-query")] public string InputFormat { get; set; } = "plain";
    [Required, MaxLength(500)] public string QueryInstruction { get; set; } = "Given a web search query, retrieve relevant passages that answer the query";
    [MaxLength(64)] public string Revision { get; set; } = "";
    /// <summary>Empty uses the Ollama inference endpoint.</summary>
    [HttpEndpoint] public string Endpoint { get; set; } = "";
    [Range(5, 300)] public int TimeoutSeconds { get; set; } = 60;
    [Range(1, 100000)] public int MaxDailyRequests { get; set; } = 20000;
    [Range(1, 128)] public int BatchSize { get; set; } = 16;
    [Range(1, 8)] public int MaxConcurrentBatches { get; set; } = 1;
    public bool AutoActivate { get; set; }
    [Range(0, 365)] public int RetiredRetentionDays { get; set; } = 7;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!VectorDimensions.Supported.Contains(Dimensions)) yield return new($"{nameof(Dimensions)} must be one of {string.Join(", ", VectorDimensions.Supported)}.", [nameof(Dimensions)]);
        if (InputFormat != "plain" && Provider != "ollama") yield return new($"{nameof(InputFormat)} qwen-query needs the ollama provider.", [nameof(InputFormat)]);
    }
}

public sealed class IndexingOptions : IValidatableObject
{
    [Range(1, 100)] public int MaxCollections { get; set; } = 30;
    [Range(1, 1000)] public int MaxDocumentsPerCollection { get; set; } = 100;
    public int ChunkTargetTokens { get; set; } = 450;
    [Range(80, 2000)] public int ChunkMaxTokens { get; set; } = 700;
    [Range(1, 2000)] public int ChunkMinTokens { get; set; } = 80;
    [Range(0d, 0.3d)] public double ChunkOverlapRatio { get; set; } = .12;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ChunkMinTokens > ChunkTargetTokens || ChunkTargetTokens > ChunkMaxTokens)
            yield return new($"{nameof(ChunkMinTokens)} ≤ {nameof(ChunkTargetTokens)} ≤ {nameof(ChunkMaxTokens)} is required.", [nameof(ChunkTargetTokens)]);
    }
}

public sealed class RetrievalOptions : IValidatableObject
{
    [AllowedValues("hybrid", "vector", "keyword")] public string Mode { get; set; } = "hybrid";
    [Range(1, 200)] public int VectorCandidates { get; set; } = 40;
    [Range(1, 200)] public int FtsCandidates { get; set; } = 40;
    [Range(1, 1000)] public int RrfK { get; set; } = 60;
    [Range(0d, 10d, MinimumIsExclusive = true)] public double VectorWeight { get; set; } = 1;
    [Range(0d, 10d, MinimumIsExclusive = true)] public double FtsWeight { get; set; } = 1;
    [Range(1, 200)] public int RerankCandidates { get; set; } = 30;
    [Range(1, 20)] public int TopK { get; set; } = 6;
    [Range(1, 20)] public int MaxChunksPerDocument { get; set; } = 3;
    /// <summary>0 keeps every vector hit.</summary>
    [Range(-1d, 1d)] public double MinVectorScore { get; set; }
    [Range(100, 16000)] public int ContextTokens { get; set; } = 3500;
    /// <summary>0 disables the query vector cache.</summary>
    [Range(0, 60)] public int QueryCacheMinutes { get; set; } = 10;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (TopK > RerankCandidates) yield return new($"{nameof(TopK)} must not exceed {nameof(RerankCandidates)}.", [nameof(TopK)]);
    }
}

public sealed class RerankOptions : IValidatableObject
{
    [AllowedValues("none", "tei", "openai-compatible")] public string Provider { get; set; } = "none";
    [HttpEndpoint] public string Endpoint { get; set; } = "";
    [Required, MaxLength(160)] public string Model { get; set; } = "bge-reranker-v2-m3";
    [Range(1, 60)] public int TimeoutSeconds { get; set; } = 10;
    [Range(double.MinValue, double.MaxValue)] public double MinScore { get; set; }
    [AllowedValues("skip", "fail")] public string FailurePolicy { get; set; } = "skip";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Provider != "none" && Endpoint.Length == 0) yield return new($"{nameof(Endpoint)} is required unless {nameof(Provider)} is none.", [nameof(Endpoint)]);
    }
}

public sealed class QueryRewriteOptions
{
    public bool Enabled { get; set; } = true;
    [Range(1, 10)] public int MaxTurns { get; set; } = 4;
    [Range(1, 30)] public int TimeoutSeconds { get; set; } = 5;
}

[OptionsValidator]
public sealed partial class KnowledgeOptionsValidator : IValidateOptions<KnowledgeOptions>;

public static class VectorDimensions
{
    public static IReadOnlyList<int> Supported { get; } = Array.AsReadOnly(new[] { 768, 1024 });
    public static string Table(int dimensions) => dimensions switch
    {
        768 => "[knowledge].[ChunkEmbeddings768]", 1024 => "[knowledge].[ChunkEmbeddings1024]",
        _ => throw new ArgumentOutOfRangeException(nameof(dimensions))
    };
}
