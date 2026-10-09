using System.Text;
using AiNexus.Platform.Errors;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace AiNexus.Features.Attachments;

public sealed class DocumentExtractor(IOptions<AttachmentOptions> options)
{
    public static readonly string[] Extensions = [".png", ".jpg", ".jpeg", ".webp", ".pdf", ".docx", ".xlsx", ".pptx", ".txt", ".md", ".csv", ".tsv", ".json", ".log", ".xml", ".yaml", ".yml", ".toml", ".ini", ".html", ".css", ".cs", ".ts", ".tsx", ".js", ".jsx", ".mjs", ".cjs", ".py", ".sql"];

    public Result<(string Type, string? Text)> Extract(string name, byte[] data, CancellationToken ct)
    {
        try { return ExtractOrReject(name, data, ct); }
        catch (DocumentRejectedException rejected) { return rejected.Error; }
    }

    // Limits are checked deep inside the parsers, so they unwind with DocumentRejectedException; it never leaves this class.
    private (string Type, string? Text) ExtractOrReject(string name, byte[] data, CancellationToken ct)
    {
        var extension = Path.GetExtension(name).ToLowerInvariant();
        if (!Extensions.Contains(extension)) throw new DocumentRejectedException(AttachmentsErrors.FileTypeUnsupported);
        ct.ThrowIfCancellationRequested();
        if (extension is ".png" or ".jpg" or ".jpeg" or ".webp")
        {
            var valid = extension switch
            {
                ".png" => data.Length >= 24 && data.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
                ".webp" => data.Length >= 16 && Encoding.ASCII.GetString(data, 0, 4) == "RIFF" && Encoding.ASCII.GetString(data, 8, 4) == "WEBP",
                _ => data.Length >= 4 && data[0] == 255 && data[1] == 216 && data[2] == 255 && data[^2] == 255 && data[^1] == 217
            };
            if (!valid) throw new DocumentRejectedException(AttachmentsErrors.InvalidImage);
            return (extension == ".png" ? "image/png" : extension == ".webp" ? "image/webp" : "image/jpeg", null);
        }
        try
        {
            string text;
            if (extension == ".pdf")
            {
                using var pdf = PdfDocument.Open(data);
                if (pdf.NumberOfPages > options.Value.MaxPdfPages) throw new DocumentRejectedException(AttachmentsErrors.PdfPageLimit);
                var buffer = new StringBuilder();
                foreach (var page in pdf.GetPages())
                {
                    ct.ThrowIfCancellationRequested();
                    buffer.AppendLine(ContentOrderTextExtractor.GetText(page));
                    RequireLength(buffer.Length);
                }
                text = buffer.ToString();
            }
            else if (extension is ".docx" or ".xlsx" or ".pptx")
            {
                using var office = new OfficeTextExtractor(data, options.Value.MaxExtractedCharacters, ct);
                text = office.Extract(extension);
            }
            else text = new UTF8Encoding(false, true).GetString(data).TrimStart('\uFEFF');
            RequireLength(text.Length);
            if (string.IsNullOrWhiteSpace(text) && extension != ".pdf") throw new DocumentRejectedException(AttachmentsErrors.DocumentHasNoText);
            if (text.Contains('\0')) throw new InvalidDataException();
            return (extension switch {
                ".pdf" => "application/pdf",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                _ => "text/plain" }, text);
        }
        catch (Exception ex) when (ex is not DocumentRejectedException && ex is not OperationCanceledException)
        {
            throw new DocumentRejectedException(AttachmentsErrors.DocumentUnreadable);
        }
    }

    private void RequireLength(int length)
    {
        if (length > options.Value.MaxExtractedCharacters) throw new DocumentRejectedException(AttachmentsErrors.DocumentTooLarge);
    }
}

/// <summary>An expected rejection raised inside the document parsers; <see cref="DocumentExtractor.Extract"/> returns its error.</summary>
internal sealed class DocumentRejectedException(Error error) : Exception(error.Code)
{
    public Error Error { get; } = error;
}
