using AiNexus.Features.Collaboration;

namespace AiNexus.Features.Quality.Evaluations;

public sealed record EvaluationSetDto(ResourceDto Resource, string Description, IReadOnlyList<EvaluationCase> Cases, int Version);
