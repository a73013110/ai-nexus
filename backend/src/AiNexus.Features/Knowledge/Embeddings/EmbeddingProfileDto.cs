using AiNexus.Features.Jobs;

namespace AiNexus.Features.Knowledge.Embeddings;

public sealed record EmbeddingProfileDto(int Id, string Key, string Provider, string Model, int Dimensions, string Status,
    DateTimeOffset CreatedAt, DateTimeOffset? ActivatedAt, DateTimeOffset? RetiredAt, EmbeddingCoverageDto Coverage, JobDto? Job);
