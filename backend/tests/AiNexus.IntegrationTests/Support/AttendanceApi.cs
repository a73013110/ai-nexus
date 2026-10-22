using System.Net.Http.Json;
using AiNexus.Data.App;
using AiNexus.Data.App.AccessControl;
using AiNexus.Data.App.Attendance;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Staff.Attendance.Cards;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AiNexus.IntegrationTests.Support;

/// <summary>出勤卡號對應 API 的測試捷徑。Seed 沒有 HR 群組，測試自己建一個只含卡號對應功能的群組。</summary>
internal static class AttendanceApi
{
    internal const string Cards = "/api/v1/attendance-cards";
    internal const string Assignments = "/api/v1/attendance-card-assignments";
    internal const string Unattributed = "/api/v1/attendance-unattributed-cards";

    /// <summary>打卡機的時區：刷卡時間固定 +08:00。</summary>
    internal static readonly TimeSpan Taipei = TimeSpan.FromHours(8);

    internal static DateTimeOffset At(int month, int day, int hour = 8, int minute = 0) => new(2026, month, day, hour, minute, 0, Taipei);

    /// <summary>建立只含卡號對應頁面的測試群組（不用 Seed 的 <c>hr</c>，它含出勤的全部功能）並授予 <paramref name="userId"/>；<paramref name="maintain"/> 為 false 時只能讀。</summary>
    internal static async Task GrantCardsAsync(AppFactory factory, Guid userId, bool maintain = true)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var suffix = maintain ? "test-hr" : "test-hr-readonly";
        db.Set<Role>().Add(new() { Id = suffix, Name = "人資" });
        db.Set<RoleGroup>().Add(new() { Id = suffix, Name = "人資" });
        db.Set<RoleGroupRole>().Add(new() { RoleId = suffix, GroupId = suffix });
        db.Set<RoleGroupFeature>().Add(new() { GroupId = suffix, FeatureId = FeatureCatalog.AttendanceCards });
        if (maintain) db.Set<RoleGroupFeature>().Add(new() { GroupId = suffix, FeatureId = FeatureCatalog.AttendanceCardsMaintain });
        db.Set<UserRole>().Add(new() { UserId = userId, RoleId = suffix });
        await db.SaveChangesAsync();
    }

    internal static async Task<CardDto> CreateCardAsync(HttpClient client, string number, string kind = CardKinds.Employee, string? note = null)
    {
        var response = await client.PostAsJsonAsync(Cards, new CardCreateRequest(number, kind, note));
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<CardDto>())!;
    }

    internal static Task<HttpResponseMessage> AssignAsync(HttpClient client, Guid cardId, Guid? userId, DateTimeOffset startsAt, DateTimeOffset? endsAt = null, bool takeOver = false, string? holderName = null)
        => client.PostAsJsonAsync(Assignments, new CardAssignmentCreateRequest(cardId, userId, holderName, startsAt, endsAt, null, takeOver));

    internal static async Task<CardAssignmentDto> AssignOkAsync(HttpClient client, Guid cardId, Guid? userId, DateTimeOffset startsAt, DateTimeOffset? endsAt = null, bool takeOver = false, string? holderName = null)
    {
        var response = await AssignAsync(client, cardId, userId, startsAt, endsAt, takeOver, holderName);
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<CardAssignmentDto>())!;
    }

    internal static async Task<CardAssignmentDto[]> HistoryAsync(HttpClient client, Guid cardId)
        => (await client.GetFromJsonAsync<CardAssignmentDto[]>($"{Assignments}?cardId={cardId}"))!;

    /// <summary>直接寫入刷卡（匯入尚未實作）；每個測試資料庫共用一個檔案列。</summary>
    internal static async Task AddPunchesAsync(AppFactory factory, params (string Card, DateTimeOffset At)[] punches)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var file = await db.Set<PunchFile>().FirstOrDefaultAsync();
        if (file is null)
        {
            file = new PunchFile { FileName = "20261001.txt" };
            db.Add(file);
            await db.SaveChangesAsync();
        }
        db.AddRange(punches.Select(x => new Punch
        {
            PunchFileId = file.Id,
            CardNumber = x.Card,
            PunchedAt = x.At,
            AttendanceDate = DateOnly.FromDateTime(x.At.DateTime),
            MachineCode = "001",
            EventCode = "11",
        }));
        await db.SaveChangesAsync();
    }

    internal static async Task<IReadOnlyList<(string Result, string Details)>> AuditAsync(AppFactory factory, string action)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rows = await db.AuditEvents.AsNoTracking().Where(x => x.Action == action).OrderBy(x => x.Id).Select(x => new { x.Result, x.DetailsJson }).ToListAsync();
        return [.. rows.Select(x => (x.Result ?? "", x.DetailsJson ?? ""))];
    }
}

/// <summary>一個 host、一位持有卡號對應全部功能的 HR、兩位一般使用者與一張員工卡，供持有期間的測試共用。</summary>
internal sealed class HrSession : IAsyncDisposable
{
    public required AppFactory Factory { get; init; }
    public required HttpClient Hr { get; init; }
    public required Guid Bob { get; init; }
    public required Guid Carol { get; init; }
    public required CardDto Card { get; init; }

    public static async Task<HrSession> CreateAsync(AppFactory? factory = null)
    {
        factory ??= new AppFactory();
        var bob = await OrganizationApi.UserIdAsync(factory, "bob");
        var carol = await OrganizationApi.UserIdAsync(factory, "carol");
        await AttendanceApi.GrantCardsAsync(factory, await OrganizationApi.UserIdAsync(factory, "hr"));
        var hr = await factory.SignedInAsync("hr");
        return new() { Factory = factory, Hr = hr, Bob = bob, Carol = carol, Card = await AttendanceApi.CreateCardAsync(hr, "A001") };
    }

    public async ValueTask DisposeAsync()
    {
        Hr.Dispose();
        await Factory.DisposeAsync();
    }
}
