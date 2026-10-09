using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Inference;

public sealed class RunRecoveryWorker(IServiceScopeFactory scopes, GenerationScheduler scheduler, StorageReadiness storage, ILogger<RunRecoveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                if (!storage.Configured || !scheduler.Ready) continue;
                // Lease renewal and the cancel check are single atomic statements; recovery locks each run's conversation itself.
                try
                {
                    using var scope = scopes.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
                    var now = DateTimeOffset.UtcNow;
                    var tracked = scheduler.TrackedRuns;
                    if (tracked.Length > 0)
                    {
                        await db.Runs.Where(x => tracked.Contains(x.Id) && x.ExecutorId == scheduler.InstanceId && x.ActiveOwnerId != null)
                            .ExecuteUpdateAsync(p => p.SetProperty(x => x.LeaseExpiresAt, now + GenerationScheduler.LeaseDuration), stoppingToken);
                        var active = await db.Runs.Where(x => tracked.Contains(x.Id) && x.ActiveOwnerId != null).Select(x => x.Id).ToArrayAsync(stoppingToken);
                        foreach (var id in tracked.Except(active)) scheduler.Cancel(id);
                    }
                    await scope.ServiceProvider.GetRequiredService<RunLeaseRecovery>().RecoverAsync(now, stoppingToken);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested) { logger.LogWarning("Orphan recovery postponed ({ErrorType}).", ex.GetType().Name); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
