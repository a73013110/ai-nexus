using AiNexus.Features.Identity;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Attachments;

/// <summary>Metadata of one of the caller's attachments.</summary>
internal static class GetAttachment
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/{id:guid}", async (Guid id, ICurrentUser user, AttachmentService files, CancellationToken ct) =>
        {
            var file = await files.FindOwnedAsync(user.Id, id, ct);
            return file.IsSuccess ? Results.Ok(AttachmentService.Describe(file.Value)) : file.Error.ToProblem();
        })
        .WithName("GetAttachment").Produces<AttachmentDto>();
}
