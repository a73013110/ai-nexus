using System.Text;
using AiNexus.Features.Attachments;
using AiNexus.Features.Collaboration;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Knowledge.Documents;

/// <summary>純文字來源：以 .txt 附件儲存並與其他文件一樣建立索引，內容仍可編輯。</summary>
internal static class TextDocuments
{
    public const int MaxCharacters = 64000;

    /// <summary>回傳標題已去除空白的請求；若有規則不符，回傳第一個錯誤。</summary>
    public static Result<TextDocumentRequest> Clean(TextDocumentRequest request)
    {
        var named = ResourceAccess.Name(request.Title);
        if (!named.IsSuccess) return named.Error;
        var title = named.Value;
        if (title.IndexOfAny(['/', '\\', ':', '*', '?', '"', '<', '>', '|']) >= 0 || title.Any(char.IsControl))
            return KnowledgeErrors.TextTitleInvalid;
        if (string.IsNullOrWhiteSpace(request.Text) || request.Text.Length > MaxCharacters || request.Text.Contains('\0'))
            return KnowledgeErrors.TextContentInvalid;
        return request with { Title = title };
    }

    public static async Task<Result<AttachmentDto>> UploadAsync(AttachmentService attachments, Guid actor, TextDocumentRequest request, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(request.Text);
        using var stream = new MemoryStream(bytes);
        var file = new FormFile(stream, 0, bytes.Length, "file", request.Title + ".txt") { Headers = new HeaderDictionary(), ContentType = "text/plain" };
        return await attachments.UploadAsync(actor, file, ct);
    }
}
