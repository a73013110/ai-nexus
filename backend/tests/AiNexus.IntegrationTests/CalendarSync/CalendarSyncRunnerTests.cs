using System.Net;
using System.Net.Http.Json;
using AiNexus.Data.App;
using AiNexus.Data.App.CalendarSync;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Office.CalendarSync;
using AiNexus.Features.Office.CalendarSync.Management;
using AiNexus.Features.Office.CalendarSync.Sync;
using AiNexus.Features.Platform.Gd;
using AiNexus.Features.Platform.Google;
using AiNexus.Features.Platform.Google.Calendar;
using AiNexus.Platform.Data;
using AiNexus.Platform.Data.Tables;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AiNexus.IntegrationTests.CalendarSync;

/// <summary>同步一輪的完整流程：假 GD → 規劃 → 資料庫 → 假 Google 日曆（記憶體）；端點經 host 呼叫。</summary>
public sealed class CalendarSyncRunnerTests
{
    private const string CalendarId = "team@example.test";
    private static readonly DateTime Day = DateTime.Today.AddDays(3);

    private sealed class Scenario(AppFactory factory, FakeGd gd, MemoryCalendar google)
    {
        public AppFactory Factory { get; } = factory;
        public FakeGd Gd { get; } = gd;
        public MemoryCalendar Google { get; } = google;

        public async Task<CalendarSyncOutcome> RunAsync(bool manual = false, bool confirmRemovals = false)
        {
            using var scope = Factory.Services.CreateScope();
            var request = manual || confirmRemovals ? new CalendarSyncRequest(Guid.Empty, confirmRemovals) : null;
            return await scope.ServiceProvider.GetRequiredService<CalendarSyncRunner>().RunAsync(request, TestContext.Current.CancellationToken);
        }

        public async Task<CalendarSyncOutcome> ResetAsync()
        {
            using var scope = Factory.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<CalendarSyncRunner>().ResetAsync(new(Guid.Empty, false), TestContext.Current.CancellationToken);
        }

        public async Task<List<CalendarEvent>> RowsAsync()
        {
            using var scope = Factory.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<AppDbContext>().CalendarEvents.AsNoTracking().OrderBy(x => x.DesNo).ToListAsync();
        }
    }

    private static async Task RunAsync(Func<Scenario, Task> test, bool enabled = false)
    {
        using var gd = FakeGd.Create();
        gd.AddUser("G001", "alice", name: "王大明");
        gd.AddUser("G002", "bob", name: "林小芳");
        var google = new MemoryCalendar();
        // 啟用時啟動檢查會讀服務帳戶金鑰；給一把臨時的（日曆呼叫由記憶體日曆接手，不會用到它）。
        var key = Path.Combine(Path.GetTempPath(), $"ainexus-google-{Guid.NewGuid():N}.json");
        using (var rsa = System.Security.Cryptography.RSA.Create(2048))
            await File.WriteAllTextAsync(key, System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["type"] = "service_account",
                ["client_email"] = "sync@example-project.iam.gserviceaccount.test",
                ["private_key"] = rsa.ExportPkcs8PrivateKeyPem(),
            }), TestContext.Current.CancellationToken);
        try
        {
            await RunWithAsync(gd, google, key, enabled, test);
        }
        finally
        {
            File.Delete(key);
        }
    }

    private static async Task RunWithAsync(FakeGd gd, MemoryCalendar google, string key, bool enabled, Func<Scenario, Task> test)
    {
        await using var factory = new AppFactory(services: services =>
        {
            services.PostConfigure<GoogleOptions>(options => options.ServiceAccountKeyFile = key);
            services.PostConfigure<DatabasesOptions>(options => options[GdDatabase.Name] = gd.Databases[GdDatabase.Name]);
            services.PostConfigure<CalendarSyncOptions>(options =>
            {
                options.CalendarId = CalendarId;
                options.Enabled = enabled;
                options.RemovalHoldMinimum = 3;
                options.RateLimitPauseSeconds = 1;
            });
            services.RemoveAll<IGoogleCalendar>();
            services.AddSingleton<IGoogleCalendar>(google);
        });
        await test(new Scenario(factory, gd, google));
    }

    [Fact]
    public async Task GdFormsBecomeEventsAndFollowLaterChanges()
    {
        await RunAsync(async s =>
        {
            s.Gd.AddLeave("L-1", "G001", "A", Day.AddHours(8.5), Day.AddHours(18), "Send");
            s.Gd.Update("Form_Leave", "L-1", "AgentUserId = @agent", new { agent = "G002" });
            s.Gd.AddTravel("T-1", "G002", Day.AddHours(9), Day.AddHours(17), "拜訪客戶", "Decide");
            // 退回結案不是核准，不同步。
            s.Gd.AddLeave("L-2", "G001", "A", Day.AddHours(8.5), Day.AddHours(12), "Finish");

            var first = await s.RunAsync();

            Assert.Equal((2, 0, 0), (first.Created, first.Updated, first.Deleted));
            Assert.Equal(["【出差】林小芳", "【請假】王大明"], s.Google.Events.Values.Select(x => x.Summary).Order());
            var leave = s.Google.Events.Values.Single(x => x.Summary == "【請假】王大明");
            Assert.Equal("代理人：林小芳\n狀態：簽核中\n文號：L-1", leave.Description);
            Assert.All(await s.RowsAsync(), x => Assert.Equal(CalendarEventStates.Synced, x.State));

            // 沒有變化的一輪不推送。
            Assert.Equal((0, 0, 0), (await s.RunAsync() is var idle ? (idle.Created, idle.Updated, idle.Deleted) : default));

            s.Gd.Update("Form_Leave", "L-1", "FlowOver = 'Decide'");
            s.Gd.Update("Form_Travel", "T-1", "FlowOver = 'Cancel'");
            var second = await s.RunAsync();

            Assert.Equal((0, 1, 1), (second.Created, second.Updated, second.Deleted));
            Assert.Contains("狀態：已核准", Assert.Single(s.Google.Events.Values).Description);
            var rows = await s.RowsAsync();
            Assert.Equal(CalendarEventStates.Removed, rows.Single(x => x.DesNo == "T-1").State);
            Assert.NotNull(rows.Single(x => x.DesNo == "T-1").RemovedAt);

            // 撤回的單又送出：救回同一列、重建同一個事件 ID。
            s.Gd.Update("Form_Travel", "T-1", "FlowOver = 'Send'");
            var third = await s.RunAsync();
            Assert.Equal(1, third.Created + third.Updated);
            Assert.Equal(2, s.Google.Events.Count);
            Assert.Equal(2, (await s.RowsAsync()).Count);
        });
    }

    private static async Task<HttpClient> ViewerAsync(Scenario s)
    {
        var bob = await OrganizationApi.UserIdAsync(s.Factory, "bob");
        await AttendanceApi.GrantAsync(s.Factory, bob, "test-calendar-sync", FeatureCatalog.CalendarSync);
        return await s.Factory.SignedInAsync("bob");
    }

    private static async Task<List<CalendarSyncRunItemDto>> ItemsAsync(HttpClient viewer, Guid runId)
    {
        var page = await viewer.GetFromJsonAsync<TablePage<CalendarSyncRunItemDto>>($"/api/v1/calendar-sync/runs/{runId}/items", TestContext.Current.CancellationToken);
        return [.. page!.Items];
    }

    [Fact]
    public async Task EachRunRecordsWhatHappenedToEveryEventAndWhy()
    {
        await RunAsync(async s =>
        {
            var viewer = await ViewerAsync(s);
            s.Gd.AddLeave("L-1", "G001", "A", Day.AddHours(8.5), Day.AddHours(18), "Send");
            s.Gd.AddLeave("L-2", "G002", "A", Day.AddHours(8.5), Day.AddHours(12), "Decide");
            s.Gd.AddLeave("L-3", "G002", "A", Day.AddHours(13), Day.AddHours(18), "Decide");
            var first = await s.RunAsync();
            var created = await ItemsAsync(viewer, first.Run!.Id);
            Assert.All(created, x => Assert.Equal((SyncRunItemOutcomes.Created, null), (x.Outcome, x.Cause)));
            Assert.Equal(first.Created, created.Count);

            s.Gd.Update("Form_Leave", "L-1", "FlowOver = 'Decide'");
            s.Gd.Update("Form_Leave", "L-2", "FlowOver = 'Cancel'");
            s.Gd.Remove("Form_Leave", "L-3");
            var second = await s.RunAsync(confirmRemovals: true);
            var items = (await ItemsAsync(viewer, second.Run!.Id)).ToDictionary(x => x.DesNo);

            Assert.Equal(new CalendarEventFieldChange("flowStatus", "Send", "Decide"), Assert.Single(items["L-1"].Changes));
            Assert.Equal((SyncRunItemOutcomes.Updated, CalendarEventCauses.Content), (items["L-1"].Outcome, items["L-1"].Cause));
            Assert.Equal((SyncRunItemOutcomes.Deleted, CalendarEventCauses.FormStatus), (items["L-2"].Outcome, items["L-2"].Cause));
            Assert.Equal(new CalendarEventFieldChange("flowStatus", "Decide", "Cancel"), Assert.Single(items["L-2"].Changes));
            Assert.Equal((SyncRunItemOutcomes.Deleted, CalendarEventCauses.FormMissing), (items["L-3"].Outcome, items["L-3"].Cause));
            Assert.Equal("王大明", items["L-1"].ApplicantName);
            Assert.Equal(30, second.Run.LookbackDays);

            Assert.All(await s.RowsAsync(), x => Assert.Null(x.PendingCause));
        });
    }

    [Fact]
    public async Task AFailedPushIsListedWithItsErrorAndKeepsItsCauseUntilItSucceeds()
    {
        await RunAsync(async s =>
        {
            var viewer = await ViewerAsync(s);
            s.Gd.AddLeave("L-1", "G001", "A", Day.AddHours(8.5), Day.AddHours(18), "Send");
            await s.RunAsync();
            s.Gd.Update("Form_Leave", "L-1", "FlowOver = 'Decide'");
            s.Google.RejectDescriptionContaining = "L-1";

            var failed = await s.RunAsync();
            var item = Assert.Single(await ItemsAsync(viewer, failed.Run!.Id));
            Assert.Equal((SyncRunItemOutcomes.Failed, CalendarSyncFailures.GoogleRejected), (item.Outcome, item.ErrorCode));
            Assert.NotNull(item.ErrorMessage);
            Assert.Equal(CalendarEventCauses.Content, Assert.Single(await s.RowsAsync()).PendingCause);

            s.Google.RejectDescriptionContaining = null;
            var retried = await s.RunAsync(manual: true);
            var success = Assert.Single(await ItemsAsync(viewer, retried.Run!.Id));
            Assert.Equal(new CalendarEventFieldChange("flowStatus", "Send", "Decide"), Assert.Single(success.Changes));
            Assert.Null(success.ErrorCode);
        });
    }

    [Fact]
    public async Task HeldRemovalsAreListedBeforeTheyAreConfirmed()
    {
        await RunAsync(async s =>
        {
            var viewer = await ViewerAsync(s);
            foreach (var n in Enumerable.Range(1, 4)) s.Gd.AddLeave($"L-{n}", "G001", "A", Day.AddHours(8.5), Day.AddHours(18), "Decide");
            await s.RunAsync();
            foreach (var n in Enumerable.Range(1, 4)) s.Gd.Update("Form_Leave", $"L-{n}", "FlowOver = 'Cancel'");

            var held = await s.RunAsync();

            var items = await ItemsAsync(viewer, held.Run!.Id);
            Assert.Equal(4, items.Count);
            Assert.All(items, x => Assert.Equal((SyncRunItemOutcomes.Held, CalendarEventCauses.FormStatus), (x.Outcome, x.Cause)));
            Assert.All(await s.RowsAsync(), x => Assert.Null(x.PendingCause));
        });
    }

    [Fact]
    public async Task ItemsOfAnUnknownRunAreNotFound()
    {
        await RunAsync(async s =>
        {
            var viewer = await ViewerAsync(s);

            await ProblemAssertions.AssertCodeAsync(await viewer.GetAsync($"/api/v1/calendar-sync/runs/{Guid.NewGuid()}/items"), HttpStatusCode.NotFound, "calendar_sync_run_not_found");
        });
    }

    [Fact]
    public async Task TheEventListShowsOnlyWhatSyncStillManagesUnlessHistoryIsAsked()
    {
        await RunAsync(async s =>
        {
            s.Gd.AddLeave("L-1", "G001", "A", Day.AddHours(8.5), Day.AddHours(18), "Decide");
            s.Gd.AddLeave("L-2", "G002", "A", Day.AddHours(8.5), Day.AddHours(12), "Decide");
            await s.RunAsync();
            s.Gd.Update("Form_Leave", "L-2", "FlowOver = 'Cancel'");
            await s.RunAsync();
            var ct = TestContext.Current.CancellationToken;
            using (var scope = s.Factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var old = DateTimeOffset.UtcNow.AddDays(-400);
                db.CalendarEvents.Add(new CalendarEvent
                {
                    CalendarId = CalendarId, DesNo = "L-9", Kind = CalendarEventKinds.Leave, ApplicantName = "陳小華", FlowStatus = "Decide",
                    StartsAt = old, EndsAt = old.AddHours(1), ContentHash = new byte[CalendarEvent.HashLength], GoogleEventId = "old-9",
                    State = CalendarEventStates.Synced, CreatedAt = old, UpdatedAt = old
                });
                await db.SaveChangesAsync(ct);
            }
            var viewer = await ViewerAsync(s);

            async Task<string[]> DesNosAsync(string query)
                => [.. (await viewer.GetFromJsonAsync<TablePage<CalendarSyncEventDto>>("/api/v1/calendar-sync/events" + query, ct))!.Items.Select(x => x.DesNo).Order()];
            Assert.Equal(["L-1"], await DesNosAsync(""));
            Assert.Equal(["L-1", "L-2", "L-9"], await DesNosAsync("?history=true"));
        });
    }

    [Fact]
    public async Task EachSegmentOfAMultiPeriodLeaveIsItsOwnEvent()
    {
        await RunAsync(async s =>
        {
            // GD 的實際樣子：主表是整段期間、DayOfWeek 存星期代碼；各段在 Form_Leave_Group，DayOfWeek 存中文星期。
            s.Gd.AddLeave("L-1", "G001", "A", Day, Day.AddDays(14), "Decide", dayOfWeek: "3");
            s.Gd.AddLeaveGroup("L-1", Day.AddHours(17), Day.AddHours(17.5), "星期三");
            s.Gd.AddLeaveGroup("L-1", Day.AddDays(7).AddHours(17), Day.AddDays(7).AddHours(17.5), "星期三");

            Assert.Equal(2, (await s.RunAsync()).Created);

            // 鍵與舊系統 EDocSyncToGoogleCalendar.DayOfWeek 相同（例：20250402星期三），轉入的舊列才接得上。
            var rows = (await s.RowsAsync()).OrderBy(x => x.SegmentKey, StringComparer.Ordinal).ToList();
            Assert.Equal([Day.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture) + "星期三",
                Day.AddDays(7).ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture) + "星期三"], rows.Select(x => x.SegmentKey));
            Assert.All(rows, x => Assert.Equal(TimeSpan.FromMinutes(30), x.EndsAt - x.StartsAt));
            Assert.Equal(2, s.Google.Events.Count);
        });
    }

    [Fact]
    public async Task UnavailableGdRemovesNothing()
    {
        await RunAsync(async s =>
        {
            s.Gd.AddLeave("L-1", "G001", "A", Day.AddHours(8.5), Day.AddHours(18), "Decide");
            await s.RunAsync();

            s.Gd.Databases[GdDatabase.Name].Name = "AiNexus_Test_Missing_" + Guid.NewGuid().ToString("N");
            var outcome = await s.RunAsync();

            Assert.Equal((SyncRunStatuses.Failed, CalendarSyncFailures.GdUnavailable), (outcome.Status, outcome.ErrorCode));
            Assert.Single(s.Google.Events);
            Assert.Equal(CalendarEventStates.Synced, Assert.Single(await s.RowsAsync()).State);
        });
    }

    [Fact]
    public async Task AnEventDeletedInGoogleIsRecreatedOnTheNextChange()
    {
        await RunAsync(async s =>
        {
            s.Gd.AddLeave("L-1", "G001", "A", Day.AddHours(8.5), Day.AddHours(18), "Send");
            await s.RunAsync();
            s.Google.Events.Clear();

            s.Gd.Update("Form_Leave", "L-1", "FlowOver = 'Decide'");
            var outcome = await s.RunAsync();

            Assert.Equal(1, outcome.Created);
            Assert.Equal(CalendarEvent.NewGoogleEventId(Assert.Single(await s.RowsAsync()).Id), Assert.Single(s.Google.Events.Keys));
        });
    }

    [Fact]
    public async Task MassRemovalsWaitForConfirmation()
    {
        await RunAsync(async s =>
        {
            foreach (var n in Enumerable.Range(1, 4)) s.Gd.AddLeave($"L-{n}", "G001", "A", Day.AddHours(8.5), Day.AddHours(18), "Decide");
            await s.RunAsync();
            foreach (var n in Enumerable.Range(1, 4)) s.Gd.Update("Form_Leave", $"L-{n}", "FlowOver = 'Cancel'");

            var held = await s.RunAsync();
            Assert.Equal((SyncRunStatuses.Held, 4), (held.Status, held.Held));
            Assert.Equal(4, s.Google.Events.Count);

            var confirmed = await s.RunAsync(confirmRemovals: true);
            Assert.Equal(4, confirmed.Deleted);
            Assert.Empty(s.Google.Events);
            Assert.All(await s.RowsAsync(), x => Assert.Equal(CalendarEventStates.Removed, x.State));
        });
    }

    [Fact]
    public async Task ServiceFailuresStopTheRunAndLeaveRowsForTheNextOne()
    {
        await RunAsync(async s =>
        {
            s.Gd.AddLeave("L-1", "G001", "A", Day.AddHours(8.5), Day.AddHours(18), "Decide");
            s.Gd.AddLeave("L-2", "G001", "A", Day.AddHours(8.5), Day.AddHours(18), "Decide");
            s.Google.Failure = GoogleStatus.Forbidden;

            var failed = await s.RunAsync();
            Assert.Equal((SyncRunStatuses.Failed, CalendarSyncFailures.GoogleForbidden), (failed.Status, failed.ErrorCode));
            Assert.All(await s.RowsAsync(), x => Assert.Equal((CalendarEventStates.Pending, 0), (x.State, x.Attempts)));

            s.Google.Failure = null;
            var recovered = await s.RunAsync();
            Assert.Equal(2, recovered.Created);
            Assert.Equal(SyncRunStatuses.Succeeded, recovered.Status);
        });
    }

    [Fact]
    public async Task OneRejectedEventIsRetriedLaterWithoutStoppingOthers()
    {
        await RunAsync(async s =>
        {
            s.Gd.AddLeave("L-1", "G001", "A", Day.AddHours(8.5), Day.AddHours(18), "Decide");
            s.Gd.AddLeave("L-2", "G001", "A", Day.AddHours(8.5), Day.AddHours(18), "Decide");
            s.Google.RejectDescriptionContaining = "L-2";

            var outcome = await s.RunAsync();

            Assert.Equal((SyncRunStatuses.Partial, 1, 1), (outcome.Status, outcome.Created, outcome.Failed));
            var rejected = (await s.RowsAsync()).Single(x => x.DesNo == "L-2");
            Assert.Equal((CalendarEventStates.Failed, 1, CalendarSyncFailures.GoogleRejected), (rejected.State, rejected.Attempts, rejected.LastErrorCode));
            Assert.NotNull(rejected.NextAttemptAt);

            // 排程輪等退避時間；手動同步立刻重試。
            s.Google.RejectDescriptionContaining = null;
            Assert.Equal(0, (await s.RunAsync()).Created);
            Assert.Equal(1, (await s.RunAsync(manual: true)).Created);
        });
    }

    [Fact]
    public async Task AFailedRemovalKeepsItsBackoffAndIsRevivedWhenTheFormReturns()
    {
        await RunAsync(async s =>
        {
            s.Gd.AddLeave("L-1", "G001", "A", Day.AddHours(8.5), Day.AddHours(18), "Decide");
            await s.RunAsync();
            s.Gd.Update("Form_Leave", "L-1", "FlowOver = 'Cancel'");
            s.Google.Failure = GoogleStatus.Unavailable;

            Assert.Equal(1, (await s.RunAsync()).Failed);
            var failed = Assert.Single(await s.RowsAsync());
            Assert.Equal((CalendarEventStates.Failed, 1), (failed.State, failed.Attempts));

            // 下一個排程輪還在退避：不重排、不歸零，也不再打 Google。
            Assert.Equal(0, (await s.RunAsync()).Failed);
            var waiting = Assert.Single(await s.RowsAsync());
            Assert.Equal((CalendarEventStates.Failed, 1, failed.NextAttemptAt), (waiting.State, waiting.Attempts, waiting.NextAttemptAt));

            // 單又回來：救回同一列，事件留在日曆上。
            s.Google.Failure = null;
            s.Gd.Update("Form_Leave", "L-1", "FlowOver = 'Decide'");
            Assert.Equal(1, (await s.RunAsync()).Updated);
            var revived = Assert.Single(await s.RowsAsync());
            Assert.Equal((CalendarEventStates.Synced, null), (revived.State, revived.RemovedAt));
            Assert.Single(s.Google.Events);
        });
    }

    [Fact]
    public async Task ResetDeletesThisCalendarsEventsAndRecordsAndTheNextRunStartsOver()
    {
        await RunAsync(async s =>
        {
            s.Gd.AddLeave("L-1", "G001", "A", Day.AddHours(8.5), Day.AddHours(18), "Decide");
            s.Gd.AddLeave("L-2", "G002", "A", Day.AddHours(8.5), Day.AddHours(12), "Decide");
            await s.RunAsync(manual: true);
            // 已經移除的列與另一個日曆的列、紀錄：前者一起清掉，後者不動。
            s.Gd.Update("Form_Leave", "L-2", "FlowOver = 'Cancel'");
            await s.RunAsync(manual: true);
            var ct = TestContext.Current.CancellationToken;
            using (var scope = s.Factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var now = DateTimeOffset.UtcNow;
                db.CalendarEvents.Add(new CalendarEvent
                {
                    CalendarId = "other@example.test", DesNo = "L-9", Kind = CalendarEventKinds.Leave, ApplicantName = "陳小華", FlowStatus = "Decide",
                    StartsAt = now, EndsAt = now.AddHours(1), ContentHash = new byte[CalendarEvent.HashLength], GoogleEventId = "other-9",
                    State = CalendarEventStates.Synced, CreatedAt = now, UpdatedAt = now
                });
                db.SyncRuns.Add(new SyncRun { CalendarId = "other@example.test", TriggerKind = SyncRunTriggers.Manual, StartedAt = now, FinishedAt = now, Status = SyncRunStatuses.Succeeded });
                await db.SaveChangesAsync(ct);
            }
            // Google 上手動建立的事件不是本系統記錄的，不刪。
            s.Google.Events["manual-1"] = new GoogleCalendarEvent { Id = "manual-1", Summary = "部門聚餐" };

            var reset = await s.ResetAsync();

            Assert.Equal((SyncRunStatuses.Succeeded, 1), (reset.Status, reset.Deleted));
            Assert.Equal(["manual-1"], s.Google.Events.Keys);
            Assert.Equal(["L-9"], (await s.RowsAsync()).Select(x => x.DesNo));
            using (var scope = s.Factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                Assert.Equal(["other@example.test"], await db.SyncRuns.Select(x => x.CalendarId).ToListAsync(ct));
                // 明細隨紀錄連帶刪除，保存期清理與重設都不必另外處理。
                Assert.Empty(await db.SyncRunItems.ToListAsync(ct));
            }
            Assert.Contains("\"events\":2", Assert.Single(await AttendanceApi.AuditAsync(s.Factory, CalendarSyncAudit.Cleared.Name)).Details);

            Assert.Equal(1, (await s.RunAsync()).Created);
            Assert.Equal(2, s.Google.Events.Count);
        });
    }

    [Fact]
    public async Task AResetWaitsOutGoogleRateLimitsAndGivesUpWhenTheyNeverLift()
    {
        await RunAsync(async s =>
        {
            s.Gd.AddLeave("L-1", "G001", "A", Day.AddHours(8.5), Day.AddHours(18), "Decide");
            await s.RunAsync();

            s.Google.Failure = GoogleStatus.RateLimited;
            var stopped = await s.ResetAsync();
            Assert.Equal((SyncRunStatuses.Failed, CalendarSyncFailures.GoogleRateLimited), (stopped.Status, stopped.ErrorCode));
            Assert.Single(s.Google.Events);
            Assert.Single(await s.RowsAsync());

            // 限速只擋一次：暫停後接著刪完，不算失敗。
            s.Google.Failure = null;
            s.Google.RateLimitedDeletes = 1;
            Assert.Equal(SyncRunStatuses.Succeeded, (await s.ResetAsync()).Status);
            Assert.Equal(0, s.Google.RateLimitedDeletes);
            Assert.Empty(s.Google.Events);
            Assert.Empty(await s.RowsAsync());
        });
    }

    [Fact]
    public async Task AResetThatGoogleRefusesKeepsTheRowsAndReportsWhy()
    {
        await RunAsync(async s =>
        {
            s.Gd.AddLeave("L-1", "G001", "A", Day.AddHours(8.5), Day.AddHours(18), "Decide");
            await s.RunAsync();
            s.Google.Failure = GoogleStatus.Unavailable;

            var reset = await s.ResetAsync();

            Assert.Equal((SyncRunStatuses.Failed, CalendarSyncFailures.GoogleUnavailable), (reset.Status, reset.ErrorCode));
            Assert.Single(s.Google.Events);
            var row = Assert.Single(await s.RowsAsync());
            Assert.Equal(CalendarEventStates.Failed, row.State);
            Assert.NotNull(row.RemovedAt);

            // 再按一次重設，接著把剩下的刪完。
            s.Google.Failure = null;
            Assert.Equal(SyncRunStatuses.Succeeded, (await s.ResetAsync()).Status);
            Assert.Empty(s.Google.Events);
            Assert.Empty(await s.RowsAsync());
        });
    }

    [Fact]
    public async Task AResetRequestMustNameTheConfiguredCalendar()
    {
        await RunAsync(async s =>
        {
            var bob = await OrganizationApi.UserIdAsync(s.Factory, "bob");
            await AttendanceApi.GrantAsync(s.Factory, bob, "test-calendar-sync", FeatureCatalog.CalendarSync, FeatureCatalog.CalendarSyncManage);
            var manager = await s.Factory.SignedInAsync("bob");

            await ProblemAssertions.AssertCodeAsync(await manager.PostAsJsonAsync("/api/v1/calendar-sync/reset", new { calendarId = "other@example.test" }),
                HttpStatusCode.Conflict, "calendar_sync_reset_mismatch");
            Assert.Equal(HttpStatusCode.NoContent, (await manager.PostAsJsonAsync("/api/v1/calendar-sync/reset", new { calendarId = CalendarId })).StatusCode);
            Assert.Equal(2, (await AttendanceApi.AuditAsync(s.Factory, CalendarSyncAudit.ResetRequested.Name)).Count);
        }, enabled: true);
    }

    [Fact]
    public async Task PagesAndActionsRequireTheirFeatures()
    {
        await RunAsync(async s =>
        {
            var bob = await OrganizationApi.UserIdAsync(s.Factory, "bob");
            await AttendanceApi.GrantAsync(s.Factory, bob, "test-calendar-sync", FeatureCatalog.CalendarSync);
            var carol = await s.Factory.SignedInAsync("carol");
            var viewer = await s.Factory.SignedInAsync("bob");

            Assert.Equal(HttpStatusCode.Forbidden, (await carol.GetAsync("/api/v1/calendar-sync/status")).StatusCode);
            var status = await viewer.GetFromJsonAsync<CalendarSyncStatusDto>("/api/v1/calendar-sync/status");
            Assert.Equal((false, CalendarId), (status!.Enabled, status.CalendarId));
            Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync("/api/v1/calendar-sync/calendars")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync("/api/v1/calendar-sync/events")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync("/api/v1/calendar-sync/runs")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync("/api/v1/calendar-sync/runs", new { confirmRemovals = false })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync("/api/v1/calendar-sync/reset", new { calendarId = CalendarId })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsync("/api/v1/calendar-sync/connection-test", null)).StatusCode);
        });
    }

    [Fact]
    public async Task OtherCalendarsInTheSharedDatabaseAreListedAndFilterable()
    {
        await RunAsync(async s =>
        {
            s.Gd.AddLeave("L-1", "G001", "A", Day.AddHours(8.5), Day.AddHours(18), "Decide");
            s.Gd.AddLeave("L-2", "G002", "A", Day.AddHours(8.5), Day.AddHours(12), "Decide");
            await s.RunAsync();
            // 另一個站台（或舊系統轉入）的日曆：同一個資料庫、不同的日曆 ID。
            using (var scope = s.Factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var now = DateTimeOffset.UtcNow;
                db.CalendarEvents.Add(new CalendarEvent
                {
                    CalendarId = "other@example.test", DesNo = "L-9", Kind = CalendarEventKinds.Leave, ApplicantName = "陳小華", FlowStatus = "Decide",
                    StartsAt = now, EndsAt = now.AddHours(1), ContentHash = new byte[CalendarEvent.HashLength], GoogleEventId = "legacy-9",
                    State = CalendarEventStates.Failed, CreatedAt = now, UpdatedAt = now
                });
                await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            }
            var bob = await OrganizationApi.UserIdAsync(s.Factory, "bob");
            await AttendanceApi.GrantAsync(s.Factory, bob, "test-calendar-sync", FeatureCatalog.CalendarSync);
            var viewer = await s.Factory.SignedInAsync("bob");
            var ct = TestContext.Current.CancellationToken;

            var calendars = await viewer.GetFromJsonAsync<List<CalendarSyncCalendarDto>>("/api/v1/calendar-sync/calendars", ct);
            Assert.Equal([new(CalendarId, true, 2, 0, 0), new("other@example.test", false, 1, 0, 1)], calendars);

            async Task<string[]> DesNosAsync(string query)
                => [.. (await viewer.GetFromJsonAsync<TablePage<CalendarSyncEventDto>>("/api/v1/calendar-sync/events" + query, ct))!.Items.Select(x => x.DesNo).Order()];
            Assert.Equal(["L-1", "L-2"], await DesNosAsync("?calendarId=" + Uri.EscapeDataString(CalendarId)));
            Assert.Equal(["L-9"], await DesNosAsync("?calendarId=other%40example.test"));
            Assert.Equal(["L-1", "L-2", "L-9"], await DesNosAsync(""));

            var runs = await viewer.GetFromJsonAsync<TablePage<CalendarSyncRunDto>>("/api/v1/calendar-sync/runs?calendarId=other%40example.test", ct);
            Assert.Empty(runs!.Items);
        });
    }

    [Fact]
    public async Task ManagersCanTestTheConnectionButNotSyncADisabledSite()
    {
        await RunAsync(async s =>
        {
            var bob = await OrganizationApi.UserIdAsync(s.Factory, "bob");
            await AttendanceApi.GrantAsync(s.Factory, bob, "test-calendar-sync", FeatureCatalog.CalendarSync, FeatureCatalog.CalendarSyncManage);
            var manager = await s.Factory.SignedInAsync("bob");

            var test = await (await manager.PostAsync("/api/v1/calendar-sync/connection-test", null)).Content.ReadFromJsonAsync<CalendarSyncConnectionDto>();
            Assert.Equal((true, "團隊行事曆"), (test!.Succeeded, test.CalendarSummary));

            await ProblemAssertions.AssertCodeAsync(await manager.PostAsJsonAsync("/api/v1/calendar-sync/runs", new { confirmRemovals = false }),
                HttpStatusCode.Conflict, "calendar_sync_disabled");
            Assert.Single(await AttendanceApi.AuditAsync(s.Factory, CalendarSyncAudit.ConnectionTested.Name));
        });
    }

    [Fact]
    public async Task AManualRequestIsAcceptedWhenEnabled()
    {
        await RunAsync(async s =>
        {
            var bob = await OrganizationApi.UserIdAsync(s.Factory, "bob");
            await AttendanceApi.GrantAsync(s.Factory, bob, "test-calendar-sync", FeatureCatalog.CalendarSync, FeatureCatalog.CalendarSyncManage);
            var manager = await s.Factory.SignedInAsync("bob");

            var response = await manager.PostAsJsonAsync("/api/v1/calendar-sync/runs", new { confirmRemovals = true });

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            var audit = Assert.Single(await AttendanceApi.AuditAsync(s.Factory, CalendarSyncAudit.RunRequested.Name));
            Assert.Contains("\"confirmRemovals\":true", audit.Details);
        }, enabled: true);
    }

    /// <summary>記憶體中的 Google 日曆，行為與 API 相同的部分：ID 重複 409、找不到 404。推送平行執行，所以加鎖。</summary>
    internal sealed class MemoryCalendar : IGoogleCalendar
    {
        private readonly Lock gate = new();
        public Dictionary<string, GoogleCalendarEvent> Events { get; } = [];
        public GoogleStatus? Failure { get; set; }
        public int RateLimitedDeletes { get; set; }
        public string? RejectDescriptionContaining { get; set; }

        public Task<GoogleResponse<GoogleCalendarInfo>> GetCalendarAsync(string calendarId, CancellationToken ct)
            => Task.FromResult(GoogleResponse<GoogleCalendarInfo>.Ok(new() { Summary = "團隊行事曆", TimeZone = "Asia/Taipei" }));

        public Task<GoogleResponse<GoogleCalendarEvent>> InsertEventAsync(string calendarId, GoogleCalendarEvent value, CancellationToken ct)
        {
            lock (gate)
            {
                if (Rejected(value) is { } failure) return Task.FromResult(new GoogleResponse<GoogleCalendarEvent>(failure));
                if (Events.ContainsKey(value.Id!)) return Task.FromResult(new GoogleResponse<GoogleCalendarEvent>(GoogleStatus.Conflict));
                return Task.FromResult(GoogleResponse<GoogleCalendarEvent>.Ok(Events[value.Id!] = value with { HtmlLink = "https://calendar.example.test/" + value.Id }));
            }
        }

        public Task<GoogleResponse<GoogleCalendarEvent>> UpdateEventAsync(string calendarId, string eventId, GoogleCalendarEvent value, CancellationToken ct)
        {
            lock (gate)
            {
                if (Rejected(value) is { } failure) return Task.FromResult(new GoogleResponse<GoogleCalendarEvent>(failure));
                if (!Events.ContainsKey(eventId)) return Task.FromResult(new GoogleResponse<GoogleCalendarEvent>(GoogleStatus.NotFound));
                return Task.FromResult(GoogleResponse<GoogleCalendarEvent>.Ok(Events[eventId] = value with { Id = eventId, HtmlLink = "https://calendar.example.test/" + eventId }));
            }
        }

        public Task<GoogleResponse<bool>> DeleteEventAsync(string calendarId, string eventId, CancellationToken ct)
        {
            lock (gate)
            {
                if (Failure is { } failure) return Task.FromResult(new GoogleResponse<bool>(failure));
                if (RateLimitedDeletes > 0)
                {
                    RateLimitedDeletes--;
                    return Task.FromResult(new GoogleResponse<bool>(GoogleStatus.RateLimited));
                }
                return Task.FromResult(Events.Remove(eventId) ? GoogleResponse<bool>.Ok(true) : new GoogleResponse<bool>(GoogleStatus.NotFound));
            }
        }

        public Task<GoogleResponse<IReadOnlyList<GoogleCalendarEvent>>> FindEventsAsync(string calendarId, string property, string value, CancellationToken ct)
        {
            lock (gate)
            {
                IReadOnlyList<GoogleCalendarEvent> found = [.. Events.Values.Where(x => x.ExtendedProperties?.Private?.GetValueOrDefault(property) == value)];
                return Task.FromResult(GoogleResponse<IReadOnlyList<GoogleCalendarEvent>>.Ok(found));
            }
        }

        private GoogleStatus? Rejected(GoogleCalendarEvent value)
            => Failure ?? (RejectDescriptionContaining is { } text && value.Description?.Contains(text, StringComparison.Ordinal) == true ? GoogleStatus.Invalid : null);
    }
}
