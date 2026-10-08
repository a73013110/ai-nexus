using AiNexus.Features.Persistence;
using AiNexus.Features.Conversations;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Operations;

// Only replay events expire here. Conversation retention is an explicit deployment policy.
public sealed class EventRetentionWorker(IServiceScopeFactory scopes, StorageReadiness storage, ILogger<EventRetentionWorker> logger) : BackgroundService
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
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested) { logger.LogWarning("Replay event cleanup failed ({ErrorType}).", ex.GetType().Name); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
