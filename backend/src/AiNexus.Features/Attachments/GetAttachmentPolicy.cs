using Microsoft.Extensions.Options;

namespace AiNexus.Features.Attachments;

public sealed record AttachmentPolicyDto(long MaxFileBytes, int MaxFilesPerMessage, long MaxMessageBytes, string[] Extensions);

/// <summary>Upload limits and accepted extensions for the composer.</summary>
internal sealed class GetAttachmentPolicy(IOptions<AttachmentOptions> options)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/policy", (GetAttachmentPolicy handler) => TypedResults.Ok(handler.Handle()))
        .WithName("AttachmentPolicy");

    public AttachmentPolicyDto Handle()
        => new(options.Value.MaxFileBytes, options.Value.MaxFilesPerMessage, options.Value.MaxMessageBytes, DocumentExtractor.Extensions);
}
