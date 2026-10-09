using AiNexus.Features.Identity;
using AiNexus.Platform.Errors;
using AiNexus.Features.Inference;

namespace AiNexus.Features.Chat;

/// <summary>One of the user's own runs, while its conversation is still theirs.</summary>
internal static class GetRun
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/runs/{id:guid}", async (Guid id, ICurrentUser user, RunService runs, ModelPresentation models, CancellationToken ct) =>
        {
            var run = await runs.FindOwnedAsync(user.Id, id, ct);
            return run.IsSuccess ? Results.Ok(models.Run(run.Value)) : run.Error.ToProblem();
        })
        .WithName("GetRun").Produces<RunDto>();
}
