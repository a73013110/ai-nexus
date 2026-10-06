namespace AiNexus.Modules.Knowledge;

public sealed class KnowledgeOptions
{
    public string EmbeddingProvider { get; set; } = "ollama";
    public string EmbeddingModel { get; set; } = "bge-m3";
    public int Dimensions { get; set; } = 1024;
    public string InputFormat { get; set; } = "plain";
    public string QueryInstruction { get; set; } = "Given a web search query, retrieve relevant passages that answer the query";
    public string Revision { get; set; } = "";
    public string Endpoint { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 60;
    public int MaxDailyEmbeddingRequests { get; set; } = 20000;
    public int BatchSize { get; set; } = 16;
    public int MaxConcurrentBatches { get; set; } = 1;
    public bool AutoActivate { get; set; }
    public int RetiredRetentionDays { get; set; } = 7;
    public int MaxCollections { get; set; } = 30;
    public int MaxDocumentsPerCollection { get; set; } = 100;
    public int ChunkTargetTokens { get; set; } = 450;
    public int ChunkMaxTokens { get; set; } = 700;
    public int ChunkMinTokens { get; set; } = 80;
    public double ChunkOverlapRatio { get; set; } = .12;
    public string Mode { get; set; } = "hybrid";
    public int VectorCandidates { get; set; } = 40;
    public int FtsCandidates { get; set; } = 40;
    public int RrfK { get; set; } = 60;
    public double VectorWeight { get; set; } = 1;
    public double FtsWeight { get; set; } = 1;
    public int RerankCandidates { get; set; } = 30;
    public int TopK { get; set; } = 6;
    public int MaxChunksPerDocument { get; set; } = 3;
    public double MinVectorScore { get; set; }
    public int ContextTokens { get; set; } = 3500;
    public int QueryCacheMinutes { get; set; } = 10;
    public RerankOptions Rerank { get; set; } = new();
    public QueryRewriteOptions QueryRewrite { get; set; } = new();

    public static bool Valid(KnowledgeOptions x) =>
        x.EmbeddingProvider is "google" or "ollama" or "none" && VectorDimensions.Supported.Contains(x.Dimensions)
        && x.InputFormat is "plain" or "qwen-query" && (x.InputFormat == "plain" || x.EmbeddingProvider == "ollama")
        && x.QueryInstruction.Length is > 0 and <= 500 && x.Revision.Length <= 64
        && x.EmbeddingModel.Length is > 0 and <= 160 && x.EmbeddingModel.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or ':' or '.')
        && (x.Endpoint.Length == 0 || ValidEndpoint(x.Endpoint)) && x.TimeoutSeconds is >= 5 and <= 300
        && x.MaxDailyEmbeddingRequests is >= 1 and <= 100000 && x.BatchSize is >= 1 and <= 128 && x.MaxConcurrentBatches is >= 1 and <= 8
        && x.RetiredRetentionDays is >= 0 and <= 365 && x.MaxCollections is >= 1 and <= 100 && x.MaxDocumentsPerCollection is >= 1 and <= 1000
        && x.ChunkMinTokens >= 1 && x.ChunkMinTokens <= x.ChunkTargetTokens && x.ChunkTargetTokens <= x.ChunkMaxTokens
        && x.ChunkMaxTokens is >= 80 and <= 2000 && double.IsFinite(x.ChunkOverlapRatio) && x.ChunkOverlapRatio is >= 0 and <= .3
        && x.Mode is "hybrid" or "vector" or "keyword" && x.VectorCandidates is >= 1 and <= 200 && x.FtsCandidates is >= 1 and <= 200
        && x.RrfK is >= 1 and <= 1000 && double.IsFinite(x.VectorWeight) && x.VectorWeight is > 0 and <= 10
        && double.IsFinite(x.FtsWeight) && x.FtsWeight is > 0 and <= 10 && x.RerankCandidates is >= 1 and <= 200
        && x.TopK is >= 1 and <= 20 && x.TopK <= x.RerankCandidates && x.MaxChunksPerDocument is >= 1 and <= 20
        && double.IsFinite(x.MinVectorScore) && x.MinVectorScore is >= -1 and <= 1 && x.ContextTokens is >= 100 and <= 16000
        && x.QueryCacheMinutes is >= 0 and <= 60 && x.Rerank.Provider is "none" or "tei" or "openai-compatible"
        && (x.Rerank.Provider == "none" || ValidEndpoint(x.Rerank.Endpoint)) && x.Rerank.Model.Length is > 0 and <= 160
        && x.Rerank.TimeoutSeconds is >= 1 and <= 60 && double.IsFinite(x.Rerank.MinScore) && x.Rerank.FailurePolicy is "skip" or "fail"
        && x.QueryRewrite.MaxTurns is >= 1 and <= 10 && x.QueryRewrite.TimeoutSeconds is >= 1 and <= 30;

    public static bool ValidEndpoint(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https" && uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0;
}
public sealed class RerankOptions
{
    public string Provider { get; set; } = "none";
    public string Endpoint { get; set; } = "";
    public string Model { get; set; } = "bge-reranker-v2-m3";
    public int TimeoutSeconds { get; set; } = 10;
    public double MinScore { get; set; }
    public string FailurePolicy { get; set; } = "skip";
}
public sealed class QueryRewriteOptions
{
    public bool Enabled { get; set; } = true;
    public int MaxTurns { get; set; } = 4;
    public int TimeoutSeconds { get; set; } = 5;
}
public static class VectorDimensions
{
    public static IReadOnlyList<int> Supported { get; } = Array.AsReadOnly(new[] { 768, 1024 });
    public static string Table(int dimensions) => dimensions switch
    {
        768 => "[knowledge].[ChunkEmbeddings768]", 1024 => "[knowledge].[ChunkEmbeddings1024]",
        _ => throw new ArgumentOutOfRangeException(nameof(dimensions))
    };
}
