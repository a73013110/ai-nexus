using System.Text.Json;
using AiNexus.Features.Identity;
using AiNexus.Features.Inference;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Sharing;

public sealed record SharedContentDto(ShareDto Share, ShareSnapshot Snapshot);

/// <summary>The frozen snapshot, with model ids shown as they are presented today.</summary>
internal sealed class ReadShare(ShareAccess shares, ModelPresentation presentation)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/{id:guid}", (Guid id, ICurrentUser user, ReadShare handler, CancellationToken ct) => handler.HandleAsync(user.Id, id, ct).ToHttpResultAsync());

    public async Task<Result<SharedContentDto>> HandleAsync(Guid actor, Guid id, CancellationToken ct)
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
