using AiNexus.Features.Work.Jobs;

namespace AiNexus.Features.Work.Knowledge.Embeddings;

public sealed record EmbeddingProfileDto(int Id, string Key, string Provider, string Model, int Dimensions, string Status,
    DateTimeOffset CreatedAt, DateTimeOffset? ActivatedAt, DateTimeOffset? RetiredAt, EmbeddingCoverageDto Coverage, JobDto? Job);
