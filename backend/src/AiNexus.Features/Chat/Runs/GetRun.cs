using AiNexus.Features.Identity;
using AiNexus.Features.Inference.Models;
using AiNexus.Features.Inference.Runs;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Chat.Runs;

/// <summary>One of the user's own runs, while its conversation is still theirs.</summary>
internal sealed class GetRun(RunService runs, ModelPresentation presentation)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/runs/{id:guid}", (Guid id, ICurrentUser user, GetRun handler, CancellationToken ct) => handler.HandleAsync(user.Id, id, ct).ToHttpResultAsync())
        .WithName("GetRun");

    public async Task<Result<RunDto>> HandleAsync(Guid owner, Guid id, CancellationToken ct)
    {
        var run = await runs.OwnedAsync(owner, id, ct);
        return run.IsSuccess ? presentation.Run(run.Value) : run.Error;
    }
}
