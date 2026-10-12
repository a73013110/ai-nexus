namespace AiNexus.Platform.Dashboard;

/// <summary>
/// Whose work a section counts and the reporting window. <see cref="Owner"/> is <c>null</c> for the whole platform.
/// Current state (counts, active work) ignores the window; figures marked as period figures use it, bucketed by the
/// viewer's UTC offset. Item lists (titles) always belong to <see cref="Actor"/>, whatever the scope.
/// </summary>
public sealed record DashboardScope(Guid Actor, Guid? Owner, bool Platform, DateTimeOffset From, DateTimeOffset Until, int OffsetMinutes);
