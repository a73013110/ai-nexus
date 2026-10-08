using AiNexus.Features.AccessControl;

namespace AiNexus.Features.Operations;

/// <summary>
/// The activity audit feature and its policy. Read-only investigations live under /admin/audit but are granted
/// separately from /admin management. The name is kept because <c>NexusDbContext</c> seeds the feature from it.
/// </summary>
public static class ActivityAuditEndpoints
{
    public const string Feature = "audit", Policy = Policies.Prefix + Feature;
}

public sealed record AuditDto(long Id, string Actor, string Action, Guid? ResourceId, string? Result, DateTimeOffset At, string? DetailsJson, string? ActingAs = null,
    string? Category = null, string? TraceId = null, Guid? OperationId = null, string? IssueCode = null);
