using AiNexus.Features.Work.Jobs;

namespace AiNexus.Features.Admin.Quality.RetrievalEvaluations;

public sealed record RetrievalEvaluationDto(Guid Id, string Title, int Cases, int TopK, string ProfileKey, string ConfigurationFingerprint, DateTimeOffset CreatedAt, JobDto Job);
