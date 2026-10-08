using Microsoft.Extensions.Options;

namespace AiNexus.Features.Attachments;

/// <summary>Upload limits and accepted extensions for the composer.</summary>
internal static class GetAttachmentPolicy
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/policy", (IOptions<AttachmentOptions> options) => Results.Ok(Handle(options.Value)))
        .WithName("AttachmentPolicy").Produces<AttachmentPolicyDto>();

    private static AttachmentPolicyDto Handle(AttachmentOptions options)
        => new(options.MaxFileBytes, options.MaxFilesPerMessage, options.MaxMessageBytes, DocumentExtractor.Extensions);
}
