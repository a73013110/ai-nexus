using AiNexus.Data.App.CalendarSync;
using AiNexus.Features.Office.CalendarSync.Sync;

namespace AiNexus.UnitTests.CalendarSync;

public sealed class CalendarEventChangesTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 2, 8, 30, 0, TimeSpan.FromHours(8));

    private static AbsenceSegment Segment(string flow = "Decide", string? agent = "林小芳")
        => new("L-1", "", CalendarEventKinds.Leave, "王大明", agent, flow, Start, Start.AddHours(9));

    private static CalendarEvent Row(string flow = "Send", string? agent = "林小芳", string state = CalendarEventStates.Synced, DateTimeOffset? removedAt = null) => new()
    {
        DesNo = "L-1", Kind = CalendarEventKinds.Leave, ApplicantName = "王大明", AgentName = agent, FlowStatus = flow,
        StartsAt = Start, EndsAt = Start.AddHours(9), State = state, RemovedAt = removedAt,
    };

    private static IReadOnlyList<CalendarEventFieldChange> Changes(CalendarEvent row) => CalendarEventChanges.Parse(row.PendingChangesJson);

    private static void Apply(CalendarEvent row, AbsenceSegment segment)
    {
        row.Kind = segment.Kind;
        row.ApplicantName = segment.ApplicantName;
        row.AgentName = segment.AgentName;
        row.FlowStatus = segment.FlowStatus;
    }

    [Fact]
    public void AContentChangeListsEachFieldWithItsOldAndNewValue()
    {
        var row = Row();
        CalendarEventChanges.NoteUpdate(row, Segment(agent: "陳小華"));

        Assert.Equal(CalendarEventCauses.Content, row.PendingCause);
        Assert.Equal([new("agentName", "林小芳", "陳小華"), new("flowStatus", "Send", "Decide")], Changes(row));
        Assert.Contains("陳小華", row.PendingChangesJson, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUpdateWithNoFieldDifferenceIsAFormatChange()
    {
        var row = Row("Decide");
        CalendarEventChanges.NoteUpdate(row, Segment());

        Assert.Equal(CalendarEventCauses.Content, row.PendingCause);
        Assert.Null(row.PendingChangesJson);
    }

    [Fact]
    public void AChangeNotYetPushedKeepsTheValueStillOnTheCalendar()
    {
        var row = Row("Send", agent: "林小芳");
        CalendarEventChanges.NoteUpdate(row, Segment("Decide"));
        Apply(row, Segment("Decide"));

        CalendarEventChanges.NoteUpdate(row, Segment("Decide", agent: "陳小華"));
        Assert.Equal([new("flowStatus", "Send", "Decide"), new("agentName", "林小芳", "陳小華")], Changes(row));

        Apply(row, Segment("Decide", agent: "陳小華"));
        CalendarEventChanges.NoteUpdate(row, Segment("Send"));
        Assert.Null(row.PendingChangesJson);
    }

    [Fact]
    public void ARestoredRowDropsTheReasonItWasRemoved()
    {
        var row = Row("Decide", state: CalendarEventStates.Failed, removedAt: Start);
        CalendarEventChanges.NoteRemoval(row, new Dictionary<string, string> { ["L-1"] = "Cancel" });

        CalendarEventChanges.NoteUpdate(row, Segment());

        Assert.Equal(CalendarEventCauses.Restored, row.PendingCause);
        Assert.Null(row.PendingChangesJson);
    }

    [Theory]
    [InlineData("Cancel", CalendarEventCauses.FormStatus)]
    [InlineData("Back", CalendarEventCauses.FormStatus)]
    [InlineData("Finish", CalendarEventCauses.FormStatus)]
    [InlineData("Send", CalendarEventCauses.SegmentGone)]
    [InlineData("Decide", CalendarEventCauses.SegmentGone)]
    public void ARemovalExplainsTheFormsCurrentStatus(string flow, string cause)
    {
        var row = Row("Decide");
        CalendarEventChanges.NoteRemoval(row, new Dictionary<string, string> { ["L-1"] = flow });

        Assert.Equal(cause, row.PendingCause);
        if (cause == CalendarEventCauses.FormStatus) Assert.Equal([new("flowStatus", "Decide", flow)], Changes(row));
        else Assert.Null(row.PendingChangesJson);
    }

    [Fact]
    public void ARemovalDistinguishesAMissingFormFromAnUnreadableSource()
    {
        var missing = Row();
        CalendarEventChanges.NoteRemoval(missing, new Dictionary<string, string>());
        Assert.Equal(CalendarEventCauses.FormMissing, missing.PendingCause);

        var unreadable = Row();
        CalendarEventChanges.NoteRemoval(unreadable, null);
        Assert.Equal(CalendarEventCauses.Unknown, unreadable.PendingCause);
    }

    [Fact]
    public void ALegacyRowAlreadyInThatStatusDoesNotShowAChangeToItself()
    {
        var row = Row("Finish");
        CalendarEventChanges.NoteRemoval(row, new Dictionary<string, string> { ["L-1"] = "Finish" });

        Assert.Equal([new("flowStatus", null, "Finish")], Changes(row));
    }

    [Fact]
    public void EveryFieldChangingStillFitsTheColumn()
    {
        var name = new string('王', CalendarEvent.NameMaxLength);
        var row = Row();
        CalendarEventChanges.NoteUpdate(row, new("L-1", "", CalendarEventKinds.Travel, name, name, "Decide", Start.AddDays(1), Start.AddDays(2)));

        Assert.True(row.PendingChangesJson!.Length <= CalendarEvent.ChangesMaxLength);
    }
}
