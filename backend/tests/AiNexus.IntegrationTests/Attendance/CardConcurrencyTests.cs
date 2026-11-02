using System.Net;
using System.Net.Http.Json;
using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Office.Attendance;
using AiNexus.Features.Office.Attendance.Cards;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.AttendanceApi;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;

namespace AiNexus.IntegrationTests.Attendance;

public sealed class CardConcurrencyTests
{
    private const string Conflict = "admin_write_conflict";
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(30);

    // 兩位 HR 開著同一張卡：後存的人看到的是舊資料，不能悄悄蓋掉先存的內容。
    [Fact]
    public async Task SavingACardReadBeforeSomeoneElseChangedItIsRefused()
    {
        await using var session = await HrSession.CreateAsync();
        var (hr, card) = (session.Hr, session.Card);

        (await hr.PutAsJsonAsync($"{Cards}/{card.Id}", new CardUpdateRequest(false, "先存的", card.RowVersion))).EnsureSuccessStatusCode();
        await AssertCodeAsync(await hr.PutAsJsonAsync($"{Cards}/{card.Id}", new CardUpdateRequest(true, "後存的", card.RowVersion)), HttpStatusCode.Conflict, Conflict);
        await AssertCodeAsync(await hr.DeleteAsync($"{Cards}/{card.Id}?rowVersion={card.RowVersion}"), HttpStatusCode.Conflict, Conflict);

        var saved = Assert.Single((await hr.GetFromJsonAsync<CardDto[]>(Cards))!);
        Assert.Equal(("先存的", false), (saved.Note, saved.HiddenFromRecords));
        Assert.NotEqual(card.RowVersion, saved.RowVersion);
        // 被拒絕的嘗試也留下紀錄；重新讀過之後就能存。
        Assert.Equal(["completed", Conflict], (await AuditAsync(session.Factory, "attendance.card.updated")).Select(x => x.Result));
        (await hr.PutAsJsonAsync($"{Cards}/{card.Id}", new CardUpdateRequest(true, "後存的", saved.RowVersion))).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ChangingAPeriodReadBeforeSomeoneElseChangedItIsRefused()
    {
        await using var session = await HrSession.CreateAsync();
        var (hr, bob, card) = (session.Hr, session.Bob, session.Card);
        var period = await AssignOkAsync(hr, card.Id, bob, At(10, 1));

        (await hr.PutAsJsonAsync($"{Assignments}/{period.Id}", new CardAssignmentUpdateRequest(bob, null, At(10, 1), At(10, 5), null, period.RowVersion))).EnsureSuccessStatusCode();
        await AssertCodeAsync(await hr.PutAsJsonAsync($"{Assignments}/{period.Id}", new CardAssignmentUpdateRequest(bob, null, At(10, 1), At(10, 9), null, period.RowVersion)), HttpStatusCode.Conflict, Conflict);
        await AssertCodeAsync(await hr.DeleteAsync($"{Assignments}/{period.Id}?rowVersion={period.RowVersion}"), HttpStatusCode.Conflict, Conflict);

        Assert.Equal(At(10, 5), Assert.Single(await HistoryAsync(hr, card.Id)).EndsAt);
    }

    // 使用者確認接手的是畫面上那一段；確認之後那一段被改過，就不能結束使用者沒看過的內容。
    [Fact]
    public async Task TakingOverAPeriodThatChangedAfterItWasConfirmedIsRefused()
    {
        await using var session = await HrSession.CreateAsync();
        var (hr, bob, carol, card) = (session.Hr, session.Bob, session.Carol, session.Card);
        var seen = await AssignOkAsync(hr, card.Id, bob, At(10, 1));
        (await hr.PutAsJsonAsync($"{Assignments}/{seen.Id}", new CardAssignmentUpdateRequest(bob, null, At(10, 1), null, "改過", seen.RowVersion))).EnsureSuccessStatusCode();

        var request = new CardAssignmentCreateRequest(card.Id, carol, null, At(10, 5), null, null, seen.RowVersion);
        await AssertCodeAsync(await hr.PostAsJsonAsync(Assignments, request), HttpStatusCode.Conflict, Conflict);

        Assert.Null(Assert.Single(await HistoryAsync(hr, card.Id)).EndsAt);
    }

    [Fact]
    public async Task WritesWithoutAVersionAreInvalid()
    {
        await using var session = await HrSession.CreateAsync();
        var (hr, bob, card) = (session.Hr, session.Bob, session.Card);
        var period = await AssignOkAsync(hr, card.Id, bob, At(10, 1));

        Assert.Equal(HttpStatusCode.BadRequest, (await hr.PutAsJsonAsync($"{Cards}/{card.Id}", new CardUpdateRequest(true, null, 0))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await hr.DeleteAsync($"{Cards}/{card.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await hr.PutAsJsonAsync($"{Assignments}/{period.Id}", new CardAssignmentUpdateRequest(bob, null, At(10, 1), null, null, 0))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await hr.DeleteAsync($"{Assignments}/{period.Id}")).StatusCode);
    }

    [Fact]
    public async Task AssignmentsWaitOnlyForTheSameCard()
    {
        await using var session = await HrSession.CreateAsync();
        var (hr, bob, carol, card) = (session.Hr, session.Bob, session.Carol, session.Card);
        var other = await CreateCardAsync(hr, "A002");
        await using var held = await HoldAsync(session, AttendanceLocks.Card(card.Id));

        // 別張卡不等：如果還是整個模組一把鎖，這裡會等到逾時。
        (await AssignAsync(hr, other.Id, bob, At(10, 1)).WaitAsync(Limit, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();
        var same = AssignAsync(hr, card.Id, carol, At(10, 1));
        await Task.Delay(200, TestContext.Current.CancellationToken);
        Assert.False(same.IsCompleted);

        await held.ReleaseAsync();
        (await same.WaitAsync(Limit, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();
    }

    // 匯入寫入中：改備註與指派不受影響；只有會與匯入互相影響的新增、刪除卡片要等。
    [Fact]
    public async Task AnImportHoldsUpOnlyCreatingAndDeletingCards()
    {
        await using var session = await HrSession.CreateAsync();
        var (hr, bob, card) = (session.Hr, session.Bob, session.Card);
        await using var held = await HoldAsync(session, AttendanceLocks.PunchImport);

        (await hr.PutAsJsonAsync($"{Cards}/{card.Id}", new CardUpdateRequest(true, "匯入中", card.RowVersion)).WaitAsync(Limit, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();
        (await AssignAsync(hr, card.Id, bob, At(10, 1)).WaitAsync(Limit, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();
        var creating = hr.PostAsJsonAsync(Cards, new CardCreateRequest("A002", false, null), TestContext.Current.CancellationToken);
        await Task.Delay(200, TestContext.Current.CancellationToken);
        Assert.False(creating.IsCompleted);

        await held.ReleaseAsync();
        (await creating.WaitAsync(Limit, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();
    }

    /// <summary>另一個連線持有鎖，模擬正在寫入的另一筆工作；釋放前後才看得出誰在等它。</summary>
    private static async Task<HeldLock> HoldAsync(HrSession session, string name)
    {
        var scope = session.Factory.Services.CreateAsyncScope();
        var transaction = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        Assert.True(await scope.ServiceProvider.GetRequiredService<AdministrativeWriteLock>().TryAcquireNamedAsync(name, TestContext.Current.CancellationToken));
        return new(async () =>
        {
            await transaction.DisposeAsync();
            await scope.DisposeAsync();
        });
    }

    private sealed class HeldLock(Func<Task> release) : IAsyncDisposable
    {
        private bool released;

        public async Task ReleaseAsync()
        {
            if (released) return;
            released = true;
            await release();
        }

        // 斷言失敗時也要放開，否則等鎖的請求會讓 host 關不掉。
        public async ValueTask DisposeAsync() => await ReleaseAsync();
    }
}
