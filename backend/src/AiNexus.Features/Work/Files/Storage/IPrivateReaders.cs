namespace AiNexus.Features.Work.Files.Storage;

/// <summary>只用來顯示擁有者附件的獨立文件。</summary>
public sealed class PrivateReader
{
    public Guid Id { get; init; }
    public Guid OwnerId { get; init; }
}

/// <summary>
/// 私人讀取器（含已刪除的）。知識庫模組實作此介面，讓附件清理能把「只被擁有者的讀取器引用」的檔案視為草稿，
/// 而不必相依知識庫模組。
/// </summary>
public interface IPrivateReaders
{
    IQueryable<PrivateReader> All { get; }

    /// <summary>在呼叫端的交易內，刪除擁有者對 <paramref name="files"/> 的讀取器，連同其內容、引用與進行中的工作。</summary>
    Task RemoveAsync(Guid owner, IReadOnlyList<Guid> files, CancellationToken ct);
}
