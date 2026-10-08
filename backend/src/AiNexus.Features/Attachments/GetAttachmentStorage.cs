using AiNexus.Features.Identity;

namespace AiNexus.Features.Attachments;

/// <summary>The caller's stored bytes and effective limit.</summary>
internal static class GetAttachmentStorage
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/storage", async (ICurrentUser user, AttachmentQuota quota, CancellationToken ct) => Results.Ok(await quota.ForAsync(user.Id, ct)))
        .WithName("GetAttachmentStorage").Produces<AttachmentStorageDto>();
}
