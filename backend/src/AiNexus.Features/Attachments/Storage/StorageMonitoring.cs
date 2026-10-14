using AiNexus.Features.Persistence;
using AiNexus.Platform.Monitoring;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Attachments.Storage;

/// <summary>
/// Runtime monitoring of stored files: count and bytes of every owner's ready files, and free space on the storage
/// volume. Degraded below <see cref="LowFreePercent"/>, failing below <see cref="CriticalFreePercent"/> percent free.
/// </summary>
internal sealed class StorageMonitoring(NexusDbContext db, IOptions<AttachmentOptions> options) : IMonitoringSource
{
    public const double LowFreePercent = 10, CriticalFreePercent = 3;

    public string Id => "storage";
    public string Name => "儲存空間";
    public int Order => 70;
    public TimeSpan Refresh => TimeSpan.FromMinutes(5);

    public async Task<SubsystemReading> ReadAsync(CancellationToken ct)
    {
        var stored = await db.Set<Attachment>().AsNoTracking().Where(x => x.StorageState == AttachmentStates.Ready).GroupBy(x => 1)
            .Select(g => new { Files = g.Count(), Bytes = g.Sum(x => x.Size) }).SingleOrDefaultAsync(ct);
        var (free, total) = Volume(options.Value.StoragePath);
        var percent = free is { } f && total > 0 ? Math.Round(100d * f / total.Value, 1) : (double?)null;
        var (status, summary) =
            percent < CriticalFreePercent ? (SubsystemStates.Failing, $"附件磁碟只剩 {percent}% 可用空間，上傳可能失敗。")
            : percent < LowFreePercent ? (SubsystemStates.Degraded, $"附件磁碟只剩 {percent}% 可用空間。")
            : (SubsystemStates.Healthy, percent is null ? "無法讀取附件磁碟的可用空間。" : $"附件磁碟尚有 {percent}% 可用空間。");
        var tone = percent < LowFreePercent ? SubsystemTones.Danger : SubsystemTones.Neutral;
        IReadOnlyList<SubsystemFact> facts = [
            SubsystemFact.Count("files", "檔案", stored?.Files ?? 0),
            SubsystemFact.Bytes("stored_bytes", "檔案大小", stored?.Bytes ?? 0),
            SubsystemFact.Bytes("free_bytes", "磁碟可用", free, tone),
            SubsystemFact.Percent("free_percent", "可用比例", percent, tone),
        ];
        return new(status, summary, facts);
    }

    /// <summary>Free and total bytes of the volume holding <paramref name="path"/>; unknown for paths DriveInfo cannot read (such as UNC shares).</summary>
    private static (long? Free, long? Total) Volume(string path)
    {
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path))!);
            return drive.IsReady ? (drive.AvailableFreeSpace, drive.TotalSize) : (null, null);
        }
        catch (Exception error) when (error is ArgumentException or IOException or UnauthorizedAccessException) { return (null, null); }
    }
}
