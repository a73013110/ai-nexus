using AiNexus.Data.App;
using AiNexus.Data.App.Files;
using AiNexus.Data.App.Knowledge;
using AiNexus.Features.Work.Files;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Knowledge.Documents;

/// <summary>由容器授權的唯讀檔案預覽：檔案描述與逐頁擷取文字。</summary>
public sealed record FilePreviewDto(AttachmentDto File, IReadOnlyList<DocumentPageDto> Pages);

/// <summary>
/// 分享與管理員稽核檢視共用的檔案預覽。授權由呼叫端以容器本身的引用判斷（分享、對話），
/// 這裡只組出內容，所以不會因為讀者對原始附件沒有存取權而失敗。
/// </summary>
public static class FilePreviews
{
    /// <summary>附件已建成知識文件時用文件的逐頁文字；否則把附件擷取的全文當作第 1 頁。</summary>
    public static async Task<FilePreviewDto> ReadAsync(AppDbContext db, Attachment file, CancellationToken ct)
    {
        var documentId = await db.Set<Document>().AsNoTracking()
            .Where(x => x.AttachmentId == file.Id && !x.IsDeleted && x.Status == "ready")
            .OrderBy(x => x.Id).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
        IReadOnlyList<DocumentPageDto> pages = documentId is Guid doc
            ? await db.Set<DocumentPage>().AsNoTracking().Where(x => x.DocumentId == doc).OrderBy(x => x.PageNumber)
                .Select(x => new DocumentPageDto(x.PageNumber, x.Text, x.Extraction, x.NeedsReview)).ToArrayAsync(ct)
            : string.IsNullOrWhiteSpace(file.ExtractedText) ? [] : [new DocumentPageDto(1, file.ExtractedText, "attachment", false)];
        return new FilePreviewDto(AttachmentService.Describe(file), pages);
    }
}
