using System.Text.Json;
using AiNexus.Features.Identity;
using AiNexus.Features.Inference.Models;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Sharing.Viewing;

public sealed record SharedContentDto(ShareDto Share, ShareSnapshot Snapshot);

/// <summary>凍結的快照，模型 ID 以目前對外呈現的樣子顯示。</summary>
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
