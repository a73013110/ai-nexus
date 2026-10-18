using AiNexus.Data.App.Jobs;
using AiNexus.Data.App.Quality;
using AiNexus.Features.Jobs;

namespace AiNexus.Features.Quality.Evaluations;

public sealed record EvaluationRunDto(Guid Id, Guid SetId, string Title, int SetVersion, bool CanControl, DateTimeOffset CreatedAt, JobDto Job);

internal static class EvaluationRunDtoMapping
{
    public static EvaluationRunDto ToDto(this EvaluationRun run, BackgroundJob job, Guid actor) =>
        new(run.Id, run.SetId, run.SetTitle, run.SetVersion, run.OwnerId == actor, run.CreatedAt, JobService.Describe(job));
}
