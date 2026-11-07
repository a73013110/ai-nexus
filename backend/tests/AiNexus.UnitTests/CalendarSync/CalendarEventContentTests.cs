using AiNexus.Data.App.CalendarSync;
using AiNexus.Features.Office.CalendarSync.Sync;

namespace AiNexus.UnitTests.CalendarSync;

public sealed class CalendarEventContentTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 2, 8, 30, 0, TimeSpan.FromHours(8));
    private static readonly AbsenceSegment Leave = new("A001", "", CalendarEventKinds.Leave, "王大明", "林小芳", "Decide", Start, Start.AddHours(9.5));

    [Fact]
    public void RendersTheSummaryDescriptionAndRowReference()
    {
        var row = Guid.NewGuid();
        var value = CalendarEventContent.Render(row, CalendarEvent.NewGoogleEventId(row), Leave);
        Assert.Equal("【請假】王大明", value.Summary);
        Assert.Equal("代理人：林小芳\n狀態：已核准\n文號：A001", value.Description);
        Assert.Equal(row.ToString("N"), value.Id);
        Assert.Equal("1", value.ColorId);
        Assert.Equal(row.ToString("N"), value.ExtendedProperties!.Private![CalendarEventContent.RowProperty]);
        Assert.Equal("4", CalendarEventContent.Render(row, null, Leave with { Kind = CalendarEventKinds.Travel }).ColorId);
    }

    [Fact]
    public void TheHashFollowsWhatGoogleShows()
    {
        var hash = CalendarEventContent.Hash(Leave);
        Assert.Equal(32, hash.Length);
        Assert.Equal(hash, CalendarEventContent.Hash(Leave with { }));
        Assert.NotEqual(hash, CalendarEventContent.Hash(Leave with { AgentName = "陳志強" }));
        Assert.NotEqual(hash, CalendarEventContent.Hash(Leave with { FlowStatus = "Send" }));
        Assert.NotEqual(hash, CalendarEventContent.Hash(Leave with { EndsAt = Leave.EndsAt.AddMinutes(30) }));
        // 段的鍵不在事件內容裡，換鍵不需要重推。
        Assert.Equal(hash, CalendarEventContent.Hash(Leave with { SegmentKey = "20261102" }));
    }
}
