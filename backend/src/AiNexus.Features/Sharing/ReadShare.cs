using System.Text.Json;
using AiNexus.Features.Identity;
using AiNexus.Features.Inference;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Sharing;

/// <summary>The frozen snapshot, with model ids shown as they are presented today.</summary>
internal static class ReadShare
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/{id:guid}", async (Guid id, ICurrentUser user, ShareAccess shares, ModelPresentation presentation, CancellationToken ct) =>
            (await HandleAsync(shares, presentation, user.Id, id, ct)).ToHttpResult())
        .Produces<SharedContentDto>();

    public static async Task<Result<SharedContentDto>> HandleAsync(ShareAccess shares, ModelPresentation presentation, Guid actor, Guid id, CancellationToken ct)
    {
        var share = await shares.RequireAsync(actor, id, ct);
        if (!share.IsSuccess) return share.Error;
        var snapshot = JsonSerializer.Deserialize<ShareSnapshot>(share.Value.SnapshotJson)!;
        return new SharedContentDto(await shares.DescribeAsync(actor, share.Value, ct), snapshot with
        {
            Messages = snapshot.Messages.Select(x => x with { ModelId = x.ModelId is null ? null : presentation.PublicId(x.ModelId), ModelDisplayName = presentation.DisplayName(x.ModelId) }).ToArray(),
        });
    }
}
