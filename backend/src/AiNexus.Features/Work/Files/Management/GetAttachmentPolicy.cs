using AiNexus.Features.Work.Files.Extraction;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Modules;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Work.Files.Management;

public sealed record AttachmentPolicyDto(long MaxFileBytes, int MaxFilesPerMessage, long MaxMessageBytes, string[] Extensions);

/// <summary>輸入框使用的上傳上限與接受的副檔名。</summary>
internal sealed class GetAttachmentPolicy(IOptions<FilesOptions> options) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapGet("/policy", (GetAttachmentPolicy handler) => handler.Handle())
        .NotAudited("上傳限制")
        .WithName("AttachmentPolicy");

    public AttachmentPolicyDto Handle()
        => new(options.Value.MaxFileBytes, options.Value.MaxFilesPerMessage, options.Value.MaxMessageBytes, DocumentExtractor.Extensions);
}
