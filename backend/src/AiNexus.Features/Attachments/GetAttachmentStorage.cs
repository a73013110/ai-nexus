using AiNexus.Features.Identity;

namespace AiNexus.Features.Attachments;

/// <summary>The caller's stored bytes and effective limit.</summary>
internal sealed class GetAttachmentStorage(AttachmentQuota quota)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/storage", async (ICurrentUser user, GetAttachmentStorage handler, CancellationToken ct) => TypedResults.Ok(await handler.HandleAsync(user.Id, ct)))
        .WithName("GetAttachmentStorage");

    public Task<AttachmentStorageDto> HandleAsync(Guid owner, CancellationToken ct) => quota.ForAsync(owner, ct);
}
