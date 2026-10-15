using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Chat.Runs;

// Only replay events expire here. Conversation retention is an explicit deployment policy.
public sealed partial class RunEventRetentionWorker(IServiceScopeFactory scopes, StorageReadiness storage, ILogger<RunEventRetentionWorker> logger, TimeProvider clock) : BackgroundService
{
    /// <summary>
    /// Rows per DELETE. Each batch commits on its own, so no statement nears SQL Server's lock escalation (about 5,000
    /// locks) or holds the log for the whole backlog.
    /// </summary>
    public const int BatchSize = 1000;

    public static readonly TimeSpan Retention = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                if (!storage.Configured) continue;
                try { await CleanupAsync(stoppingToken); }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested) { LogCleanupFailed(logger, ex.GetType().Name); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    /// <summary>Deletes, batch by batch until none are left, the expired events of runs that are no longer active; returns how many.</summary>
    public async Task<int> CleanupAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        var cutoff = clock.GetUtcNow() - Retention;
        var expired = db.RunEvents.Where(x => x.CreatedAt < cutoff && db.Runs.Any(r => r.Id == x.RunId && r.ActiveOwnerId == null));
        var total = 0;
        while (true)
        {
            var deleted = await expired.Take(BatchSize).ExecuteDeleteAsync(ct);
            total += deleted;
            if (deleted < BatchSize) return total;
        }
    }

    [LoggerMessage(EventId = 3004, EventName = "replay.cleanup_failed", Level = LogLevel.Warning, Message = "Replay event cleanup failed ({ErrorType}).")]
    private static partial void LogCleanupFailed(ILogger logger, string errorType);
}
