using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AiNexus.Data.App;
using AiNexus.Data.App.AccessControl;
using AiNexus.Data.App.Attendance;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Integrations.Sources;
using AiNexus.Features.Office.Attendance.Records;
using AiNexus.Platform.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace AiNexus.IntegrationTests.Support;

/// <summary>
/// 出勤紀錄的測試環境：一個假 GD、一個時鐘固定在 <see cref="Now"/> 的 host。假 GD 的行事曆有 10/19～10/23 五個上班日，
/// 所以 19～21 日是已結束的平日、22 日是今天、23 日是未來。卡片、持有期間與刷卡直接寫入資料庫，不經卡號對應 API。
/// </summary>
internal sealed class RecordsSession : IAsyncDisposable
{
    internal const string Records = "/api/v1/attendance-records";
    internal const string Week = "from=2026-10-19&to=2026-10-23";

    /// <summary>查詢當下：2026-10-22（週四）10:00 台灣時間。</summary>
    internal static readonly DateTimeOffset Now = new(2026, 10, 22, 10, 0, 0, AttendanceApi.Taipei);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public required FakeGd Gd { get; init; }
    public required AppFactory Factory { get; init; }

    /// <param name="gdReachable">false 時 GD 連線指向不存在的資料庫：行事曆與假單都讀不到。</param>
    /// <param name="services">額外的服務設定，例如 <see cref="RequestQueries.Register"/>。</param>
    public static RecordsSession Create(bool gdReachable = true, Action<IServiceCollection>? services = null)
    {
        var gd = FakeGd.Create();
        var factory = new AppFactory(clock: new FakeTimeProvider(Now), services: collection =>
        {
            collection.PostConfigure<DatabasesOptions>(options =>
                options[GdDatabase.Name] = gdReachable ? gd.Databases[GdDatabase.Name] : FakeGd.Missing());
            services?.Invoke(collection);
        });
        for (var day = 19; day <= 23; day++) gd.AddDay(new(2026, 10, day));
        return new() { Gd = gd, Factory = factory };
    }

    internal static DateTimeOffset At(int day, int hour, int minute = 0, int second = 0) => new(2026, 10, day, hour, minute, second, AttendanceApi.Taipei);

    /// <summary>登入建立使用者並設定同名 AD 帳號；<paramref name="mapped"/> 時在假 GD 建公文帳號 <c>G-帳號</c>。</summary>
    public async Task<Guid> PersonAsync(string account, bool mapped = true)
    {
        var id = await OrganizationApi.UserIdAsync(Factory, account);
        await CalendarSession.SetAdAccountAsync(Factory, id, account);
        if (mapped) Gd.AddUser(GdId(account), account);
        return id;
    }

    public static string GdId(string account) => "G-" + account;

    /// <summary>登錄卡片並建立一段持有期間（預設 10/1 起持有中）；<paramref name="userId"/> 為 null 時持有人只有姓名。</summary>
    public Task HoldAsync(Guid? userId, string card, DateTimeOffset? startsAt = null, DateTimeOffset? endsAt = null, string kind = CardKinds.Employee)
        => WithDbAsync(async db =>
        {
            var entity = await db.Set<Card>().SingleOrDefaultAsync(x => x.CardNumber == card);
            if (entity is null)
            {
                entity = new Card { CardNumber = card, Kind = kind };
                db.Add(entity);
            }
            db.Add(new CardAssignment
            {
                CardId = entity.Id,
                UserId = userId,
                HolderName = userId is null ? "新進同仁" : null,
                StartsAt = startsAt ?? At(1, 0),
                EndsAt = endsAt,
            });
            await db.SaveChangesAsync();
        });

    public Task PunchAsync(string card, params DateTimeOffset[] at) => AttendanceApi.AddPunchesAsync(Factory, [.. at.Select(x => (card, x))]);

    /// <summary>授予 <c>attendance.records</c> 的範圍：每個範圍一個測試群組（第一次建立），再把群組的角色指派給 <paramref name="userId"/>。</summary>
    public Task GrantAsync(Guid userId, string scope) => WithDbAsync(async db =>
    {
        var id = "test-records-" + scope;
        if (!await db.Set<RoleGroup>().AnyAsync(x => x.Id == id))
        {
            db.Set<Role>().Add(new() { Id = id, Name = id });
            db.Set<RoleGroup>().Add(new() { Id = id, Name = id });
            db.Set<RoleGroupRole>().Add(new() { RoleId = id, GroupId = id });
            db.Set<RoleGroupFeature>().Add(new() { GroupId = id, FeatureId = FeatureCatalog.AttendanceRecords, Scope = scope });
        }
        db.Set<UserRole>().Add(new() { UserId = userId, RoleId = id });
        await db.SaveChangesAsync();
    });

    public Task ExcludeAsync(Guid userId) => WithDbAsync(async db =>
    {
        db.Add(new RosterExclusion { UserId = userId, Note = "測試排除" });
        await db.SaveChangesAsync();
    });

    public async Task WithDbAsync(Func<AppDbContext, Task> action)
    {
        using var scope = Factory.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public static async Task<AttendanceRecordsDto> GetAsync(HttpClient client, string query = Week)
    {
        var response = await client.GetAsync($"{Records}?{query}");
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<AttendanceRecordsDto>(Json))!;
    }

    /// <summary>名單上的人，依 ID 排序，方便與預期的集合比較。</summary>
    public static async Task<Guid[]> PeopleAsync(HttpClient client, string query = Week)
        => [.. (await GetAsync(client, query)).People.Select(x => x.UserId).Order()];

    public static AttendanceRecordDto Day(AttendancePersonDto person, int day) => Assert.Single(person.Days, x => x.Date == new DateOnly(2026, 10, day));

    public async ValueTask DisposeAsync()
    {
        await Factory.DisposeAsync();
        Gd.Dispose();
    }
}
