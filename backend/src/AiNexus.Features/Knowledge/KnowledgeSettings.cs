namespace AiNexus.Features.Knowledge;

/// <summary>Reads the deployment settings (<c>Knowledge</c>) into <see cref="KnowledgeOptions"/>; the settings layout is separate from the options services use.</summary>
public static class KnowledgeSettings
{
    public static void Bind(IConfiguration config, KnowledgeOptions options)
    {
        var section = config.GetSection("Knowledge");
        var embedding = section.GetSection("Embedding");
        options.EmbeddingProvider = embedding["Provider"] ?? options.EmbeddingProvider;
        options.EmbeddingModel = embedding["Model"] ?? options.EmbeddingModel;
        options.Dimensions = embedding.GetValue("Dimensions", options.Dimensions);
        options.InputFormat = embedding["InputFormat"] ?? options.InputFormat;
        options.QueryInstruction = embedding["QueryInstruction"] ?? options.QueryInstruction;
        options.Revision = embedding["Revision"] ?? options.Revision;
        options.TimeoutSeconds = embedding.GetValue("TimeoutSeconds", options.TimeoutSeconds);
        options.MaxDailyEmbeddingRequests = embedding.GetValue("MaxDailyRequests", options.MaxDailyEmbeddingRequests);
        options.Endpoint = embedding["Endpoint"] ?? options.Endpoint;
        options.BatchSize = embedding.GetValue("BatchSize", options.BatchSize);
        options.MaxConcurrentBatches = embedding.GetValue("MaxConcurrentBatches", options.MaxConcurrentBatches);
        options.AutoActivate = embedding.GetValue("AutoActivate", options.AutoActivate);
        options.RetiredRetentionDays = embedding.GetValue("RetiredRetentionDays", options.RetiredRetentionDays);
        section.GetSection("Indexing").Bind(options);
        section.GetSection("Retrieval").Bind(options);
        section.Bind(options);
    }
}
