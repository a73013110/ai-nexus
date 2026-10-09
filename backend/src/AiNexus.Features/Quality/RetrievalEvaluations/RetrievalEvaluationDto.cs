using AiNexus.Features.Jobs;

namespace AiNexus.Features.Quality.RetrievalEvaluations;

public sealed record RetrievalEvaluationDto(Guid Id, string Title, int Cases, int TopK, string ProfileKey, string ConfigurationFingerprint, DateTimeOffset CreatedAt, JobDto Job);
