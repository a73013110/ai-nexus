using AiNexus.Features.Jobs;

namespace AiNexus.Features.Quality.Evaluations;

public sealed record EvaluationRunDto(Guid Id, Guid SetId, string Title, int SetVersion, bool CanControl, DateTimeOffset CreatedAt, JobDto Job);
