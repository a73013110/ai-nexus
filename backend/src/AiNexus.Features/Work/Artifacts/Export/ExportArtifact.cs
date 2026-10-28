using System.Text;
using AiNexus.Features.Work.Artifacts.Management;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Export;

namespace AiNexus.Features.Work.Artifacts.Export;

/// <summary>
/// 下載單一版本，格式為 Word、PDF 或 Markdown；排版交給 Platform 的 <see cref="MarkdownDocument"/> 與 <see cref="PdfRenderer"/>。
/// 渲染後會再檢查一次讀取權限：PDF 渲染較慢，期間被撤銷的權限不能被繞過。
/// </summary>
internal sealed class ExportArtifact(GetArtifact reader, PdfRenderer pdf)
{
    private const string Caption = "AI NEXUS · 成果文件";

    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/{id:guid}/export/{format}", (
                Guid id, string format, int? version, ICurrentUser user, ExportArtifact handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, id, format, version, ct).ToHttpResultAsync(file => file.ToHttpResult()))
        .Audited(ArtifactsAudit.ArtifactExported)
        .WithName("ExportArtifact");

    public async Task<Result<ExportFile>> HandleAsync(Guid actor, Guid id, string format, int? version, CancellationToken ct)
    {
        var artifact = await reader.HandleAsync(actor, id, version, ct);
        if (!artifact.IsSuccess) return artifact.Error;
        var output = await RenderAsync(artifact.Value, format, ct);
        if (!output.IsSuccess) return output.Error;
        var recheck = await reader.HandleAsync(actor, id, artifact.Value.Version, ct);
        if (!recheck.IsSuccess) return recheck.Error;
        return output;
    }

    private async Task<Result<ExportFile>> RenderAsync(ArtifactDto document, string format, CancellationToken ct)
    {
        var (title, content) = (document.Resource.Name, document.Content);
        var name = title + "." + format;
        return format switch
        {
            "md" => new ExportFile(name, "text/markdown; charset=utf-8", Encoding.UTF8.GetBytes(content)),
            "docx" => new ExportFile(name, MarkdownDocument.DocxContentType, MarkdownDocument.Docx(title, content)),
            "pdf" => new ExportFile(name, PdfRenderer.ContentType, await pdf.RenderAsync(MarkdownDocument.Html(title, content, Caption), ct)),
            _ => ArtifactsErrors.ExportFormatInvalid,
        };
    }
}
