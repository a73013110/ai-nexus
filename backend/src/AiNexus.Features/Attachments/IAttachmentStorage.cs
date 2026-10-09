namespace AiNexus.Features.Attachments;

/// <summary>Keys are opaque immutable identifiers. Only domain services grant access to stored bytes.</summary>
public interface IAttachmentStorage
{
    Task WriteAsync(string key, ReadOnlyMemory<byte> bytes, CancellationToken ct);
    Task<Stream> OpenReadAsync(string key, CancellationToken ct);
    Task DeleteAsync(string key, CancellationToken ct);
    IAsyncEnumerable<string> StaleKeysAsync(DateTimeOffset before, CancellationToken ct);
    Task VerifyAsync(CancellationToken ct);
}
