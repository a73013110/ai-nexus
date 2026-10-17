using AiNexus.Features.Attachments;
using AiNexus.Features.Identity;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Security;

namespace AiNexus.Features.Knowledge.Documents;

/// <summary>以安全的檔案回應串流使用者可讀取文件的原始檔，可內嵌顯示或作為下載。</summary>
internal sealed class DownloadDocumentOriginal(DocumentAccess documents, AttachmentService files)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes)
    {
        routes.MapGet("/{id:guid}/render", (Guid id, HttpContext http, ICurrentUser user, DownloadDocumentOriginal handler, CancellationToken ct) =>
                handler.HandleHtmlAsync(user.Id, id, ct).ToHttpResultAsync(x => WebSecurity.SandboxedHtml(http, x)))
            .WithName("RenderDocumentHtml");
        return routes
        .MapGet("/{id:guid}/content", (Guid id, bool? download, HttpContext http, ICurrentUser user, DownloadDocumentOriginal handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, id, ct).ToHttpResultAsync(x => WebSecurity.File(http, x.Content, x.File.ContentType, x.File.FileName, download == true)))
        .WithName("DocumentOriginal");
    }

    public async Task<Result<Stream>> HandleHtmlAsync(Guid actor, Guid id, CancellationToken ct)
    {
        var original = await documents.OriginalAsync(actor, id, ct);
        if (!original.IsSuccess) return original.Error;
        if (!WebSecurity.IsHtml(original.Value.ContentType, original.Value.FileName)) return AttachmentsErrors.NotFound;
        return await files.OpenAsync(original.Value, ct);
    }

    public async Task<Result<(Attachment File, Stream Content)>> HandleAsync(Guid actor, Guid id, CancellationToken ct)
    {
        var original = await documents.OriginalAsync(actor, id, ct);
        if (!original.IsSuccess) return original.Error;
        return (original.Value, await files.OpenAsync(original.Value, ct));
    }
}
