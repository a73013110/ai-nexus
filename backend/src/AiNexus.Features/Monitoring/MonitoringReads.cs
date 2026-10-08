using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Monitoring;

internal static class MonitoringErrors
{
    public static readonly Error InvalidWindow = Error.Invalid("invalid_request");
}

/// <summary>What the administrator reads of live monitoring: a window <see cref="RuntimeTraffic"/> keeps, audited before release.</summary>
internal static class MonitoringReads
{
    public static bool ValidWindow(int minutes) => minutes is 1 or 5 or 15;

    public static async Task AuditAsync(NexusDbContext db, Guid user, string action, CancellationToken ct)
    {
        db.AuditEvents.Add(new() { OwnerId = user, Action = action, ResourceId = user, DetailsJson = "{\"scope\":\"current_instance\"}" });
        await db.SaveChangesAsync(ct);
    }
}
