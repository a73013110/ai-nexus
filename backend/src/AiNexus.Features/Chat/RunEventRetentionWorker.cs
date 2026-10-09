using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Chat;

// Only replay events expire here. Conversation retention is an explicit deployment policy.
public sealed partial class RunEventRetentionWorker(IServiceScopeFactory scopes, StorageReadiness storage, ILogger<RunEventRetentionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                if (!storage.Configured) continue;
                try
                {
                    using var scope = scopes.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
                    var cutoff = DateTimeOffset.UtcNow.AddHours(-24);
                    await db.RunEvents.Where(x => x.CreatedAt < cutoff && db.Runs.Any(r => r.Id == x.RunId && r.ActiveOwnerId == null)).ExecuteDeleteAsync(stoppingToken);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested) { LogCleanupFailed(logger, ex.GetType().Name); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    [LoggerMessage(EventId = 3004, EventName = "replay.cleanup_failed", Level = LogLevel.Warning, Message = "Replay event cleanup failed ({ErrorType}).")]
    private static partial void LogCleanupFailed(ILogger logger, string errorType);
}
