using AiNexus.Data.App.CalendarSync;
using AiNexus.Features.Office.CalendarSync.Sync;

namespace AiNexus.UnitTests.CalendarSync;

public sealed class CalendarSyncPlannerTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 2, 8, 30, 0, TimeSpan.FromHours(8));

    private static AbsenceSegment Segment(string desNo, string? agent = "林小芳", string flow = "Decide")
        => new(desNo, "", CalendarEventKinds.Leave, "王大明", agent, flow, Start, Start.AddHours(9.5));

    private static KnownEvent Known(AbsenceSegment segment, string state = CalendarEventStates.Synced, bool inWindow = true)
        => new(Guid.NewGuid(), segment.DesNo, segment.SegmentKey, CalendarEventContent.Hash(segment), state, inWindow);

    private static CalendarSyncPlan Plan(IReadOnlyList<AbsenceSegment> desired, IReadOnlyList<KnownEvent> known, bool confirm = false)
        => CalendarSyncPlanner.Plan(desired, known, holdRatio: 0.3, holdMinimum: 3, confirmRemovals: confirm);

    [Fact]
    public void NewSegmentsAreCreatedAndUnchangedOnesAreLeftAlone()
    {
        var existing = Segment("A001");
        var plan = Plan([existing, Segment("A002")], [Known(existing)]);
        Assert.Equal("A002", Assert.Single(plan.Creations).Segment.DesNo);
        Assert.Empty(plan.Changes);
        Assert.Empty(plan.Removals);
    }

    [Fact]
    public void ContentChangesAndRestoredSegmentsAreUpdated()
    {
        var approved = Segment("A001");
        var removed = Segment("A002");
        var known = new[] { Known(Segment("A001", flow: "Send")), Known(removed, CalendarEventStates.Removed, inWindow: false) };
        var plan = Plan([approved, removed], known);
        Assert.Equal(["A001", "A002"], plan.Changes.Select(x => x.Segment.DesNo).Order());
        Assert.Empty(plan.Creations);
    }

    [Fact]
    public void OnlySegmentsInsideTheWindowAreRemoved()
    {
        var inside = Known(Segment("A001"));
        var outside = Known(Segment("A002"), inWindow: false);
        var alreadyRemoved = Known(Segment("A003"), CalendarEventStates.Removed);
        var kept = Segment("A004");
        var plan = Plan([kept], [inside, outside, alreadyRemoved, Known(kept), Known(Segment("A005")), Known(Segment("A006")), Known(Segment("A007"))], confirm: true);
        Assert.Contains(inside.Id, plan.Removals);
        Assert.DoesNotContain(outside.Id, plan.Removals);
        Assert.DoesNotContain(alreadyRemoved.Id, plan.Removals);
    }

    [Fact]
    public void MassRemovalsAreHeldUntilConfirmed()
    {
        var known = Enumerable.Range(1, 5).Select(x => Known(Segment($"A00{x}"))).ToList();
        var held = Plan([Segment("A001")], known);
        Assert.Empty(held.Removals);
        Assert.Equal(4, held.HeldRemovals);

        var confirmed = Plan([Segment("A001")], known, confirm: true);
        Assert.Equal(4, confirmed.Removals.Count);
        Assert.Equal(0, confirmed.HeldRemovals);
    }

    [Fact]
    public void RemovalsBelowTheMinimumAreNeverHeld()
    {
        var known = Enumerable.Range(1, 3).Select(x => Known(Segment($"A00{x}"))).ToList();
        var plan = Plan([Segment("A001")], known);
        Assert.Equal(2, plan.Removals.Count);
        Assert.Equal(0, plan.HeldRemovals);
    }
}
