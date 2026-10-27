using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Work.Files.Management;

/// <summary>呼叫者某個附件的中繼資料。</summary>
internal sealed class GetAttachment(AttachmentService files)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/{id:guid}", (Guid id, ICurrentUser user, GetAttachment handler, CancellationToken ct) => handler.HandleAsync(user.Id, id, ct).ToHttpResultAsync())
        .Audited(FilesAudit.FileRead)
        .WithName("GetAttachment");

    public async Task<Result<AttachmentDto>> HandleAsync(Guid owner, Guid id, CancellationToken ct)
    {
        var file = await files.OwnedAsync(owner, id, ct);
        return file.IsSuccess ? AttachmentService.Describe(file.Value) : file.Error;
    }
}
