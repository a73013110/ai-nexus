using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AiNexus.Features.Persistence;

/// <summary>
/// Readiness: the Nexus connection is configured and SQL Server answers within <see cref="Timeout"/>. The endpoint is
/// anonymous, so concurrent and repeated probes share one result for <see cref="Reuse"/> and cannot load the database.
/// </summary>
public sealed class DatabaseHealthCheck(IServiceScopeFactory scopes, StorageReadiness storage, TimeProvider clock) : IHealthCheck
{
    public static readonly TimeSpan Reuse = TimeSpan.FromSeconds(5), Timeout = TimeSpan.FromSeconds(5);
    private readonly Lock gate = new();
    private Task<HealthCheckResult>? probe;
    private DateTimeOffset probedAt;

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!storage.Configured) return Task.FromResult(HealthCheckResult.Unhealthy("storage_not_configured"));
        lock (gate)
        {
            var now = clock.GetUtcNow();
            if (probe is null || (probe.IsCompleted && now - probedAt >= Reuse)) (probe, probedAt) = (ProbeAsync(), now);
            return probe;
        }
    }

    // Shared by every waiting caller, so it is bounded by its own timeout rather than any one request's token.
    private async Task<HealthCheckResult> ProbeAsync()
    {
        try
        {
            using var timeout = new CancellationTokenSource(Timeout, clock);
            await using var scope = scopes.CreateAsyncScope();
            var connected = await scope.ServiceProvider.GetRequiredService<NexusDbContext>().Database.CanConnectAsync(timeout.Token);
            return connected ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("database_unavailable");
        }
        catch (Exception) { return HealthCheckResult.Unhealthy("database_unavailable"); }
    }
}
