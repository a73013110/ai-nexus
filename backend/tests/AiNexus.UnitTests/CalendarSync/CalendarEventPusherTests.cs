using AiNexus.Data.App.CalendarSync;
using AiNexus.Features.Office.CalendarSync.Sync;
using AiNexus.Features.Platform.Google;
using AiNexus.Features.Platform.Google.Calendar;

namespace AiNexus.UnitTests.CalendarSync;

public sealed class CalendarEventPusherTests
{
    private const string Calendar = "team@example.test";
    private static readonly DateTimeOffset Start = new(2026, 11, 2, 8, 30, 0, TimeSpan.FromHours(8));
    private static readonly AbsenceSegment Leave = new("A001", "", CalendarEventKinds.Leave, "王大明", null, "Decide", Start, Start.AddHours(9.5));

    private static PushItem Item(bool created = false, bool removed = false, string? legacy = null, string? eventId = null)
    {
        var id = Guid.NewGuid();
        return new(id, Calendar, eventId ?? CalendarEvent.NewGoogleEventId(id), created, removed, legacy, Leave);
    }

    [Fact]
    public async Task ANewRowIsInsertedWithItsOwnEventId()
    {
        var google = new ScriptedCalendar();
        var item = Item();
        var outcome = await new CalendarEventPusher(google).PushAsync(item, CancellationToken.None);
        Assert.Equal(PushResult.Created, outcome.Result);
        Assert.Equal(["insert:" + item.GoogleEventId], google.Calls);
    }

    [Fact]
    public async Task AnExistingIdIsAdoptedInsteadOfDuplicated()
    {
        var google = new ScriptedCalendar { Insert = GoogleStatus.Conflict };
        var item = Item();
        var outcome = await new CalendarEventPusher(google).PushAsync(item, CancellationToken.None);
        Assert.Equal(PushResult.Created, outcome.Result);
        Assert.Equal(["insert:" + item.GoogleEventId, "update:" + item.GoogleEventId], google.Calls);
        Assert.Equal(GoogleCalendarEvent.Confirmed, google.LastBody!.Status);
    }

    [Fact]
    public async Task AnEventDeletedInGoogleIsRecreated()
    {
        var google = new ScriptedCalendar { Update = GoogleStatus.NotFound };
        var item = Item(created: true);
        var outcome = await new CalendarEventPusher(google).PushAsync(item, CancellationToken.None);
        Assert.Equal(PushResult.Created, outcome.Result);
        Assert.Equal(["update:" + item.GoogleEventId, "insert:" + item.GoogleEventId], google.Calls);
    }

    [Fact]
    public async Task DeletingAnEventThatIsAlreadyGoneSucceeds()
    {
        var google = new ScriptedCalendar { Delete = GoogleStatus.Gone };
        var outcome = await new CalendarEventPusher(google).PushAsync(Item(created: true, removed: true), CancellationToken.None);
        Assert.Equal(PushResult.Deleted, outcome.Result);
    }

    [Fact]
    public async Task LegacyEventsAreFoundByTheirUuidAndAdopted()
    {
        var google = new ScriptedCalendar { Found = [new GoogleCalendarEvent { Id = "legacy123" }] };
        var outcome = await new CalendarEventPusher(google).PushAsync(Item(legacy: "uuid-1", eventId: "legacy123"), CancellationToken.None);
        Assert.Equal(PushResult.Created, outcome.Result);
        Assert.Equal(["find:UUID=uuid-1", "update:legacy123"], google.Calls);
    }

    [Fact]
    public async Task ALegacyEventDeletedBeforeAdoptionIsCreatedAgainWithoutStoppingTheRun()
    {
        var google = new ScriptedCalendar { Found = [new GoogleCalendarEvent { Id = "legacy123" }], Update = GoogleStatus.NotFound };
        var item = Item(legacy: "uuid-1");
        var outcome = await new CalendarEventPusher(google).PushAsync(item, CancellationToken.None);
        Assert.Equal(PushResult.Created, outcome.Result);
        Assert.Equal(["find:UUID=uuid-1", "update:legacy123", "insert:" + item.GoogleEventId], google.Calls);
    }

    [Fact]
    public async Task RemovingAnUnconfirmedLegacyRowDeletesTheLegacyEvent()
    {
        var google = new ScriptedCalendar { Found = [new GoogleCalendarEvent { Id = "legacy123" }] };
        var outcome = await new CalendarEventPusher(google).PushAsync(Item(removed: true, legacy: "uuid-1", eventId: "own-id"), CancellationToken.None);
        Assert.Equal(PushResult.Deleted, outcome.Result);
        Assert.Equal(["find:UUID=uuid-1", "delete:legacy123"], google.Calls);
    }

    [Fact]
    public async Task ARejectedLegacyIdFallsBackToTheRowId()
    {
        var google = new ScriptedCalendar { Found = [], InsertRejects = "legacy-id" };
        var item = Item(legacy: "uuid-1", eventId: "legacy-id");
        var outcome = await new CalendarEventPusher(google).PushAsync(item, CancellationToken.None);
        Assert.Equal(PushResult.Created, outcome.Result);
        Assert.Equal(CalendarEvent.NewGoogleEventId(item.Id), outcome.GoogleEventId);
    }

    [Theory]
    [InlineData(GoogleStatus.Forbidden, true)]
    [InlineData(GoogleStatus.CredentialRejected, true)]
    [InlineData(GoogleStatus.RateLimited, false)]
    [InlineData(GoogleStatus.Unavailable, false)]
    public async Task ServiceWideFailuresStopTheRun(GoogleStatus status, bool stops)
    {
        var google = new ScriptedCalendar { Insert = status };
        var outcome = await new CalendarEventPusher(google).PushAsync(Item(), CancellationToken.None);
        Assert.Equal(PushResult.Failed, outcome.Result);
        Assert.Equal(stops, outcome.StopsRun);
    }

    private sealed class ScriptedCalendar : IGoogleCalendar
    {
        public GoogleStatus Insert { get; init; } = GoogleStatus.Ok;
        public GoogleStatus Update { get; init; } = GoogleStatus.Ok;
        public GoogleStatus Delete { get; init; } = GoogleStatus.Ok;
        public string? InsertRejects { get; init; }
        public IReadOnlyList<GoogleCalendarEvent> Found { get; init; } = [];
        public List<string> Calls { get; } = [];
        public GoogleCalendarEvent? LastBody { get; private set; }

        public Task<GoogleResponse<GoogleCalendarInfo>> GetCalendarAsync(string calendarId, CancellationToken ct)
            => Task.FromResult(GoogleResponse<GoogleCalendarInfo>.Ok(new GoogleCalendarInfo { Summary = "團隊", TimeZone = "Asia/Taipei" }));

        public Task<GoogleResponse<GoogleCalendarEvent>> InsertEventAsync(string calendarId, GoogleCalendarEvent value, CancellationToken ct)
        {
            Calls.Add("insert:" + value.Id);
            LastBody = value;
            var status = value.Id == InsertRejects ? GoogleStatus.Invalid : Insert;
            return Task.FromResult(Respond(status, value));
        }

        public Task<GoogleResponse<GoogleCalendarEvent>> UpdateEventAsync(string calendarId, string eventId, GoogleCalendarEvent value, CancellationToken ct)
        {
            Calls.Add("update:" + eventId);
            LastBody = value;
            return Task.FromResult(Respond(Update, value));
        }

        public Task<GoogleResponse<bool>> DeleteEventAsync(string calendarId, string eventId, CancellationToken ct)
        {
            Calls.Add("delete:" + eventId);
            return Task.FromResult(Delete == GoogleStatus.Ok ? GoogleResponse<bool>.Ok(true) : new GoogleResponse<bool>(Delete));
        }

        public Task<GoogleResponse<IReadOnlyList<GoogleCalendarEvent>>> FindEventsAsync(string calendarId, string property, string value, CancellationToken ct)
        {
            Calls.Add($"find:{property}={value}");
            return Task.FromResult(GoogleResponse<IReadOnlyList<GoogleCalendarEvent>>.Ok(Found));
        }

        private static GoogleResponse<GoogleCalendarEvent> Respond(GoogleStatus status, GoogleCalendarEvent value)
            => status == GoogleStatus.Ok ? GoogleResponse<GoogleCalendarEvent>.Ok(value with { HtmlLink = "https://calendar.example.test/e" }) : new(status);
    }
}
