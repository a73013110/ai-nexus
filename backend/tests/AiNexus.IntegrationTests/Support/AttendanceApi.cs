using System.Net.Http.Json;
using AiNexus.Data.App;
using AiNexus.Data.App.AccessControl;
using AiNexus.Data.App.Attendance;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Office.Attendance;
using AiNexus.Features.Office.Attendance.Cards;
using AiNexus.Features.Office.Attendance.Imports;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace AiNexus.IntegrationTests.Support;

/// <summary>出勤 API（卡號對應、匯入紀錄）的測試捷徑。Seed 沒有 HR 群組，測試自己建一個只含卡號對應功能的群組。</summary>
internal static class AttendanceApi
{
    internal const string Cards = "/api/v1/attendance-cards";
    internal const string Assignments = "/api/v1/attendance-card-assignments";

    /// <summary>打卡機的時區：刷卡時間固定 +08:00。</summary>
    internal static readonly TimeSpan Taipei = TimeSpan.FromHours(8);

    internal static DateTimeOffset At(int month, int day, int hour = 8, int minute = 0) => new(2026, month, day, hour, minute, 0, Taipei);

    /// <summary>建立只含卡號對應頁面的測試群組（不用 Seed 的 <c>hr</c>，它含出勤的全部功能）並授予 <paramref name="userId"/>；<paramref name="maintain"/> 為 false 時只能讀。</summary>
    internal static Task GrantCardsAsync(AppFactory factory, Guid userId, bool maintain = true)
        => maintain ? GrantAsync(factory, userId, "test-hr", FeatureCatalog.AttendanceCards, FeatureCatalog.AttendanceCardsMaintain)
            : GrantAsync(factory, userId, "test-hr-readonly", FeatureCatalog.AttendanceCards);

    internal static async Task<CardDto> CreateCardAsync(HttpClient client, string number, bool hidden = false, string? note = null)
    {
        var response = await client.PostAsJsonAsync(Cards, new CardCreateRequest(number, hidden, note));
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

    internal const string Imports = "/api/v1/attendance-imports";

    /// <summary>建立只含匯入紀錄頁面的測試群組並授予 <paramref name="userId"/>；<paramref name="reimport"/> 為 false 時只能讀紀錄、不能手動重匯。</summary>
    internal static Task GrantImportsAsync(AppFactory factory, Guid userId, bool reimport = true)
        => reimport ? GrantAsync(factory, userId, "test-imports", FeatureCatalog.AttendanceImports, FeatureCatalog.AttendanceImportsReimport)
            : GrantAsync(factory, userId, "test-imports-readonly", FeatureCatalog.AttendanceImports);

    internal const string Calendar = "/api/v1/attendance-calendar-days";

    /// <summary>建立只含假日設定頁面的測試群組並授予 <paramref name="userId"/>；<paramref name="maintain"/> 為 false 時只能查詢、不能回寫。</summary>
    internal static Task GrantCalendarAsync(AppFactory factory, Guid userId, bool maintain = true)
        => maintain ? GrantAsync(factory, userId, "test-calendar", FeatureCatalog.AttendanceCalendar, FeatureCatalog.AttendanceCalendarMaintain)
            : GrantAsync(factory, userId, "test-calendar-readonly", FeatureCatalog.AttendanceCalendar);

    /// <summary>建立同名的角色與群組，群組授予 <paramref name="features"/>，角色指派給 <paramref name="userId"/>；撤銷時以 <paramref name="id"/> 找群組。</summary>
    internal static async Task GrantAsync(AppFactory factory, Guid userId, string id, params string[] features)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Set<Role>().Add(new() { Id = id, Name = id });
        db.Set<RoleGroup>().Add(new() { Id = id, Name = id });
        db.Set<RoleGroupRole>().Add(new() { RoleId = id, GroupId = id });
        db.Set<RoleGroupFeature>().AddRange(features.Select(feature => new RoleGroupFeature { GroupId = id, FeatureId = feature }));
        db.Set<UserRole>().Add(new() { UserId = userId, RoleId = id });
        await db.SaveChangesAsync();
    }

    /// <summary>直接寫入刷卡、略過匯入（歸戶測試只關心刷卡本身），沒有卡片的卡號與匯入一樣補建；每個測試資料庫共用一個檔案列。</summary>
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
        var numbers = punches.Select(x => x.Card).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var known = await db.Set<Card>().Where(x => numbers.Contains(x.CardNumber)).Select(x => x.CardNumber).ToListAsync();
        db.AddRange(numbers.Except(known, StringComparer.OrdinalIgnoreCase).Select(number => new Card { CardNumber = number }));
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

/// <summary>暫存資料夾充當打卡機的輸出資料夾，附一個固定起點的假時鐘；測試不碰網路共用。</summary>
internal sealed class PunchSource : IDisposable
{
    public string Folder { get; } = Path.Combine(Path.GetTempPath(), $"ainexus-punches-{Guid.NewGuid():N}");
    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 10, 22, 10, 0, 0, AttendanceApi.Taipei));

    public PunchSource() => Directory.CreateDirectory(Folder);

    /// <summary><paramref name="administrators"/> 有值時改用 LDAP 登入並把這些帳號設為管理員（測試身分的情境）。</summary>
    public AppFactory CreateFactory(Action<AttendanceOptions>? configure = null, Action<IServiceCollection>? services = null, string[]? administrators = null)
        => new(ldap: administrators is not null, administrators: administrators, clock: Clock, services: collection =>
        {
            collection.PostConfigure<AttendanceOptions>(options =>
            {
                options.Import.SourceDirectory = Folder;
                configure?.Invoke(options);
            });
            services?.Invoke(collection);
        });

    public static string NameOf(int month, int day) => $"2026{month:00}{day:00}.txt";

    /// <summary>每行 <c>機器代號:卡號:yyyyMMdd:HHmmss:事件代號</c>，日期固定 2026 年。</summary>
    public static string Line(string card, int month, int day, int hour, int minute, int second = 0, string machine = "001", string eventCode = "11")
        => $"{machine}:{card}:2026{month:00}{day:00}:{hour:00}{minute:00}{second:00}:{eventCode}";

    /// <summary>寫檔並把最後修改時間設成 <paramref name="age"/> 之前（預設 1 小時，已超過 SettleSeconds）；回傳完整路徑。</summary>
    public string Write(string name, string[] lines, TimeSpan? age = null, string newline = "\r\n")
    {
        File.WriteAllText(Path.Combine(Folder, name), string.Concat(lines.Select(x => x + newline)));
        SetAge(name, age);
        return Path.Combine(Folder, name);
    }

    public void SetModified(string name, DateTimeOffset at) => File.SetLastWriteTimeUtc(Path.Combine(Folder, name), at.UtcDateTime);

    public void SetAge(string name, TimeSpan? age = null) => SetModified(name, Clock.GetUtcNow() - (age ?? TimeSpan.FromHours(1)));

    public void Dispose()
    {
        try { Directory.Delete(Folder, recursive: true); }
        catch (IOException) { }
    }
}

/// <summary>資料庫裡的一筆刷卡與它所屬的檔名。</summary>
internal sealed record PunchRow(string Card, DateTimeOffset At, string File);

/// <summary>一個 host（來源是暫存資料夾）、一位有匯入紀錄頁與手動重匯權限的 HR，以及直接驅動掃描與匯入的捷徑。</summary>
internal sealed class PunchImportSession : IAsyncDisposable
{
    public required PunchSource Source { get; init; }
    public required AppFactory Factory { get; init; }
    public required HttpClient Hr { get; init; }
    public required Guid HrId { get; init; }
    public FakeTimeProvider Clock => Source.Clock;

    public static async Task<PunchImportSession> CreateAsync(Action<AttendanceOptions>? configure = null, Action<IServiceCollection>? services = null)
    {
        var source = new PunchSource();
        var factory = source.CreateFactory(configure, services);
        var hrId = await OrganizationApi.UserIdAsync(factory, "hr");
        await AttendanceApi.GrantImportsAsync(factory, hrId);
        return new() { Source = source, Factory = factory, Hr = await factory.SignedInAsync("hr"), HrId = hrId };
    }

    /// <summary>執行一次排程掃描（hosted worker 在測試裡不啟動，由測試直接驅動）；<paramref name="log"/> 用來檢查 worker 的日誌。</summary>
    public Task<PunchScanResult> ScanAsync(LogRecorder<PunchImportWorker>? log = null)
        => ActivatorUtilities.CreateInstance<PunchImportWorker>(Factory.Services, log is null ? [] : [log]).ScanAsync(CancellationToken.None);

    public async Task<Result<PunchImport>> ReimportAsync(string name, Guid? requestedBy = null)
    {
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<PunchImporter>().ReimportAsync(name, requestedBy ?? HrId, CancellationToken.None);
    }

    public async Task<List<PunchRow>> PunchesAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rows = await (from p in db.Set<Punch>().AsNoTracking() join f in db.Set<PunchFile>() on p.PunchFileId equals f.Id select new { p.CardNumber, p.PunchedAt, f.FileName }).ToListAsync();
        return [.. rows.OrderBy(x => x.PunchedAt).ThenBy(x => x.CardNumber).Select(x => new PunchRow(x.CardNumber, x.PunchedAt, x.FileName))];
    }

    public async Task<List<PunchImport>> ImportsAsync(string? fileName = null)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var query = db.Set<PunchImport>().AsNoTracking();
        if (fileName is not null) query = query.Where(x => db.Set<PunchFile>().Any(f => f.Id == x.PunchFileId && f.FileName == fileName));
        return await query.OrderBy(x => x.StartedAt).ThenBy(x => x.FinishedAt).ToListAsync();
    }

    public async Task<PunchFile> FileAsync(string name)
    {
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<PunchFile>().AsNoTracking().SingleAsync(x => x.FileName == name);
    }

    public async ValueTask DisposeAsync()
    {
        Hr.Dispose();
        await Factory.DisposeAsync();
        Source.Dispose();
    }
}
