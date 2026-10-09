using System.Text;
using AiNexus.Features.Attachments;
using AiNexus.Features.Collaboration;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Knowledge.Documents;

/// <summary>Plain-text sources: stored as a .txt attachment and indexed like any other document; the text stays editable.</summary>
internal static class TextDocuments
{
    public const int MaxCharacters = 64000;

    /// <summary>The request with its trimmed title, or the first broken rule.</summary>
    public static Result<TextDocumentRequest> Clean(TextDocumentRequest request)
    {
        var title = ResourceAccess.Name(request.Title).OrThrow();
        if (title.IndexOfAny(['/', '\\', ':', '*', '?', '"', '<', '>', '|']) >= 0 || title.Any(char.IsControl))
            return KnowledgeErrors.TextTitleInvalid;
        // text_content_invalid has no reviewed public hint in PublicErrorCatalog, so it stays an exception; the public
        // problem is the same either way.
        if (string.IsNullOrWhiteSpace(request.Text) || request.Text.Length > MaxCharacters || request.Text.Contains('\0'))
            throw new ApiException(400, "text_content_invalid", $"請提供 1 至 {MaxCharacters:N0} 個字元的純文字內容。");
        return request with { Title = title };
    }

    public static async Task<AttachmentDto> UploadAsync(AttachmentService attachments, Guid actor, TextDocumentRequest request, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(request.Text);
        using var stream = new MemoryStream(bytes);
        var file = new FormFile(stream, 0, bytes.Length, "file", request.Title + ".txt") { Headers = new HeaderDictionary(), ContentType = "text/plain" };
        return (await attachments.UploadAsync(actor, file, ct)).OrThrow();
    }
}
