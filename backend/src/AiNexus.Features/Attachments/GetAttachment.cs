using AiNexus.Features.Identity;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Attachments;

/// <summary>Metadata of one of the caller's attachments.</summary>
internal sealed class GetAttachment(AttachmentService files)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/{id:guid}", (Guid id, ICurrentUser user, GetAttachment handler, CancellationToken ct) => handler.HandleAsync(user.Id, id, ct).ToHttpResultAsync())
        .WithName("GetAttachment");

    public async Task<Result<AttachmentDto>> HandleAsync(Guid owner, Guid id, CancellationToken ct)
    {
        var file = await files.OwnedAsync(owner, id, ct);
        return file.IsSuccess ? AttachmentService.Describe(file.Value) : file.Error;
    }
}
