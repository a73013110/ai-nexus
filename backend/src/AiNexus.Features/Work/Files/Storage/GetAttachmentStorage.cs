using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;

namespace AiNexus.Features.Work.Files.Storage;

/// <summary>呼叫者已使用的儲存位元組與生效中的上限。</summary>
internal sealed class GetAttachmentStorage(AttachmentQuota quota)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/storage", async (ICurrentUser user, GetAttachmentStorage handler, CancellationToken ct) => TypedResults.Ok(await handler.HandleAsync(user.Id, ct)))
        .NotAudited("使用者自己的容量")
        .WithName("GetAttachmentStorage");

    public Task<AttachmentStorageDto> HandleAsync(Guid owner, CancellationToken ct) => quota.ForAsync(owner, ct);
}
