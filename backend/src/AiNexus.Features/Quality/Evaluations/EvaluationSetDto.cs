using AiNexus.Data.App.Quality;
using AiNexus.Features.Collaboration;

namespace AiNexus.Features.Quality.Evaluations;

public sealed record EvaluationSetDto(ResourceDto Resource, string Description, IReadOnlyList<EvaluationCase> Cases, int Version);

internal static class EvaluationSetDtoMapping
{
    public static EvaluationSetDto ToDto(this EvaluationSet set, ResourceDto resource) => new(resource, set.Description, EvaluationJson.Parse<EvaluationCase>(set.CasesJson), set.Version);
}
