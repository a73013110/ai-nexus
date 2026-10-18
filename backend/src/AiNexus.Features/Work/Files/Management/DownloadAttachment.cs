using AiNexus.Data.App.Files;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Security;

namespace AiNexus.Features.Work.Files.Management;

/// <summary>以安全的檔案回應串流呼叫者的附件，可選擇內嵌預覽或當作下載。</summary>
internal sealed class DownloadAttachment(AttachmentService files)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes)
    {
        routes.MapGet("/{id:guid}/render", (Guid id, HttpContext http, ICurrentUser user, DownloadAttachment handler, CancellationToken ct) =>
                handler.HandleHtmlAsync(user.Id, id, ct).ToHttpResultAsync(x => WebSecurity.SandboxedHtml(http, x)))
            .WithName("RenderAttachmentHtml");
        return routes
        .MapGet("/{id:guid}/content", (Guid id, bool? download, HttpContext http, ICurrentUser user, DownloadAttachment handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, id, ct).ToHttpResultAsync(x => WebSecurity.File(http, x.Content, x.File.ContentType, x.File.FileName, download == true)))
        .WithName("DownloadAttachment");
    }

    public async Task<Result<Stream>> HandleHtmlAsync(Guid owner, Guid id, CancellationToken ct)
    {
        var file = await files.OwnedAsync(owner, id, ct);
        if (!file.IsSuccess) return file.Error;
        if (!WebSecurity.IsHtml(file.Value.ContentType, file.Value.FileName)) return FilesErrors.NotFound;
        return await files.OpenAsync(file.Value, ct);
    }

    public async Task<Result<(Attachment File, Stream Content)>> HandleAsync(Guid owner, Guid id, CancellationToken ct)
    {
        var file = await files.OwnedAsync(owner, id, ct);
        if (!file.IsSuccess) return file.Error;
        return (file.Value, await files.OpenAsync(file.Value, ct));
    }
}
