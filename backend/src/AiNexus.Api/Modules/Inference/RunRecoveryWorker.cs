using AiNexus.BuildingBlocks;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Modules.Inference;

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
                await scheduler.StateGate.WaitAsync(stoppingToken);
                try
                {
                    using var scope = scopes.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
                    var active = await db.Runs.Where(x => x.ActiveOwnerId != null).ToListAsync(stoppingToken);
                    foreach (var run in active.Where(x => !scheduler.IsTracked(x.Id)))
                        await scope.ServiceProvider.GetRequiredService<RunService>().FinishAsync(run, RunStates.Failed, "orphaned_run", stoppingToken);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested) { logger.LogWarning("Orphan recovery postponed ({ErrorType}).", ex.GetType().Name); }
                finally { scheduler.StateGate.Release(); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
