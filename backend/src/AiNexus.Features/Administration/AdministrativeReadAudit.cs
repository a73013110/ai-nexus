using System.Text.Json;
using AiNexus.Features.AccessControl;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Administration;

/// <summary>
/// Explicit, read-only administrative access to a user's data. Never weakens conversation owner checks. The actor is
/// checked before the read and again before the read is audited; the response is released only after its audit is saved.
/// </summary>
internal sealed class AdministrativeReadAudit(NexusDbContext db, AccessService access)
{
    public async Task<bool> AllowedAsync(Guid actor, CancellationToken ct)
        => (await access.ForUserAsync(actor, ct)).Features.Any(x => x.Id == AdministrationConfiguration.Feature);

    public async Task<Result> RecordAsync(Guid actor, string action, Guid resource, object details, CancellationToken ct)
    {
        if (!await AllowedAsync(actor, ct)) return AdministrationErrors.AdminRequired;
        db.AuditEvents.Add(new() { OwnerId = actor, ResourceId = resource, Action = action, Result = "read", DetailsJson = JsonSerializer.Serialize(details) });
        // The response is released only after its sensitive read is audited successfully.
        await db.SaveChangesAsync(ct);
        return Result.Success;
    }

    public static bool ValidPage(string? search, int offset) => !(search?.Length > 120 || offset < 0);
}
