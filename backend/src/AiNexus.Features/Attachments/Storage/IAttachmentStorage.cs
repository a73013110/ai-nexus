namespace AiNexus.Features.Attachments.Storage;

/// <summary>儲存鍵是不透明且不可變的識別碼；只有領域服務能授權讀取儲存的位元組。</summary>
public interface IAttachmentStorage
{
    Task WriteAsync(string key, ReadOnlyMemory<byte> bytes, CancellationToken ct);
    Task<Stream> OpenReadAsync(string key, CancellationToken ct);
    Task DeleteAsync(string key, CancellationToken ct);
    IAsyncEnumerable<string> StaleKeysAsync(DateTimeOffset before, CancellationToken ct);
    Task VerifyAsync(CancellationToken ct);
}
