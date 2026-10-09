using AiNexus.Features.Identity;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Inference;

/// <summary>Stops a queued or running answer. A finished run is returned unchanged.</summary>
internal sealed class CancelRun(GenerationScheduler scheduler, RunService runs, ModelPresentation presentation)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPost("/runs/{id:guid}/cancel", async (Guid id, ICurrentUser user, CancelRun handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, id, ct)).ToHttpResult())
        .WithName("CancelRun").Produces<RunDto>();

    // The conversation's generation lock keeps cancellation and the worker's token flushes from overwriting each other.
    public async Task<Result<RunDto>> HandleAsync(Guid owner, Guid id, CancellationToken ct)
    {
        if (await runs.ConversationOfAsync(owner, id, ct) is not Guid conversation) return InferenceErrors.RunNotFound;
        using (await scheduler.LockConversationAsync(conversation, ct))
        {
            var found = await runs.FindOwnedAsync(owner, id, ct);
            if (!found.IsSuccess) return found.Error;
            var run = found.Value;
            if (!RunStates.IsActive(run.Status)) return presentation.Run(run);
            scheduler.Cancel(id);
            await runs.FinishAsync(run, RunStates.Cancelled, null, ct);
            return presentation.Run(run);
        }
    }
}
