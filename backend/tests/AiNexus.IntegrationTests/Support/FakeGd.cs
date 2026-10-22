using AiNexus.Data.App;
using AiNexus.Data.App.Identity;
using AiNexus.Features.Admin.Integrations;
using AiNexus.Features.Admin.Integrations.Sources;
using AiNexus.Features.Staff.Attendance.Gd;
using AiNexus.Platform.Data;
using AiNexus.Platform.Data.Sql;
using AiNexus.Platform.Diagnostics;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AiNexus.IntegrationTests.Support;

/// <summary>
/// LocalDB 上的假公文系統（GD）：只建測試用到的表與欄位，型別依 <c>.local/gd-schema-notes.md</c> 查證的結構，表放在 <c>dbo</c>。
/// 資料庫由 <see cref="TestDatabase.CreateEmpty"/> 建立（collation 同 App，varchar 欄位放得下中文假別），釋放時刪除。測試不連任何真實的 GD。
/// </summary>
internal sealed class FakeGd : IDisposable
{
    private const string Schema = """
        CREATE TABLE [dbo].[Sys_User] (ID varchar(20) NOT NULL PRIMARY KEY, ACCOUNT varchar(50) NULL, NAME nvarchar(50) NULL, [Disable] bit NOT NULL DEFAULT 0);
        CREATE TABLE [dbo].[Form_LeaveKind] (LeaveId varchar(10) NOT NULL PRIMARY KEY, LeaveKind varchar(20) NULL, isLeave bit NOT NULL DEFAULT 1, isShow bit NOT NULL DEFAULT 1);
        CREATE TABLE [dbo].[Form_Leave] (DesNo varchar(20) NOT NULL PRIMARY KEY, LeaveUserId varchar(20) NULL, LeaveDepNO int NULL, LeaveKind varchar(10) NULL,
            LeaveSDate datetime NULL, LeaveEDate datetime NULL, LeaveTime float NULL, DayOfWeek varchar(20) NULL, FlowOver varchar(20) NULL);
        CREATE TABLE [dbo].[Form_Leave_Group] (DesNo nvarchar(20) NULL, LeaveSDate datetime NULL, LeaveEDate datetime NULL, DayOfWeek nvarchar(20) NULL);
        CREATE TABLE [dbo].[Form_Travel] (DesNo varchar(20) NOT NULL PRIMARY KEY, ChargeName varchar(50) NULL, ChargeDepName int NULL,
            TravelSDate smalldatetime NULL, TravelEDate smalldatetime NULL, TravelTime float NULL, TravelMain nvarchar(1000) NULL, Place nvarchar(100) NULL, FlowOver varchar(20) NOT NULL);
        CREATE TABLE [dbo].[Pub_Days] (hDate datetime NOT NULL, UserId varchar(20) NOT NULL, AM bit NOT NULL, PM bit NOT NULL, hReMark varchar(255) NULL);
        """;

    private readonly TestDatabase database = TestDatabase.CreateEmpty();

    private FakeGd()
    {
        Execute(Schema);
        Databases = OptionsFor(Name);
    }

    public string Name => database.Name;

    /// <summary>指向這個假 GD 的連線設定；測試可以改 <see cref="DatabaseOptions.Name"/> 模擬 GD 中途失效或恢復。</summary>
    public DatabasesOptions Databases { get; }

    /// <summary>回寫假日用的可寫入連線（<c>Databases:GdCalendar</c>）：同一個假 GD，不帶唯讀意圖。</summary>
    public DatabaseOptions Writable => Connection(Name, readOnly: false);

    public static FakeGd Create() => new();

    /// <summary>指向不存在的資料庫的單一連線設定，連線一定失敗。</summary>
    public static DatabaseOptions Missing(bool readOnly) => Connection("AiNexus_Test_Missing_" + Guid.NewGuid().ToString("N"), readOnly);

    /// <summary>假 GD 加上一個 App 資料庫（放使用者），給要把使用者對到公文帳號的測試；結束後兩個都刪除。</summary>
    public static async Task WithAppAsync(Func<FakeGd, AppDbContext, Task> test)
    {
        using var gd = Create();
        await TestDatabase.WithContextAsync(db => test(gd, db));
    }

    /// <summary>直接寫入一位使用者（不經 host）；<paramref name="adAccount"/> 為 null 表示還沒有 AD 帳號。</summary>
    public static Guid AddAppUser(AppDbContext db, string? adAccount)
    {
        var key = Guid.NewGuid().ToString("N");
        var user = new User { Sid = "gd-" + key, Account = "gd-" + key, DisplayName = adAccount ?? "no-account", AdAccount = adAccount, LastSeenAt = DateTimeOffset.UtcNow };
        db.Users.Add(user);
        db.SaveChanges();
        return user.Id;
    }

    /// <summary>指向不存在的資料庫，連線一定失敗；模擬 GD 連不上。</summary>
    public static DatabasesOptions Unreachable() => OptionsFor("AiNexus_Test_Missing_" + Guid.NewGuid().ToString("N"));

    /// <summary>沒有 <c>Databases:Gd</c> 設定。</summary>
    public static DatabasesOptions NotConfigured() => new();

    /// <summary>以正式環境同一個 <see cref="SqlDatabase{TDatabase}"/> 實作連到 <paramref name="databases"/> 的 <see cref="GdAccess"/>。</summary>
    public static GdAccess AccessTo(DatabasesOptions databases, ILogger<GdAccess>? logger = null)
    {
        var options = Options.Create(databases);
        return new(new SqlDatabase<GdDatabase>(options, new AuditContextSource(TimeProvider.System)), options, Options.Create(new IntegrationsOptions()), logger ?? NullLogger<GdAccess>.Instance);
    }

    public GdAccess Access(ILogger<GdAccess>? logger = null) => AccessTo(Databases, logger);

    public void AddUser(string id, string? account) => Execute("INSERT [dbo].[Sys_User] (ID, ACCOUNT, NAME) VALUES (@id, @account, @id)", new { id, account });

    public void AddDay(DateOnly day, bool morningOff = false, bool afternoonOff = false, string? remark = null, string userId = "sys")
        => Execute("INSERT [dbo].[Pub_Days] (hDate, UserId, AM, PM, hReMark) VALUES (@day, @userId, @morningOff, @afternoonOff, @remark)",
            new { day = day.ToDateTime(TimeOnly.MinValue), userId, morningOff, afternoonOff, remark = new DbString { Value = remark, IsAnsi = false, Length = 4000 } });

    /// <summary>直接讀假 GD 的一天（不經被測程式）。</summary>
    public GdDay Day(DateOnly day)
    {
        using var connection = new SqlConnection(database.ConnectionString);
        return connection.QuerySingle<GdDay>("SELECT AM AS MorningOff, PM AS AfternoonOff, hReMark AS Remark, UserId FROM [dbo].[Pub_Days] WHERE hDate = @day",
            new { day = day.ToDateTime(TimeOnly.MinValue) });
    }

    public void AddKind(string id, string name) => Execute("INSERT [dbo].[Form_LeaveKind] (LeaveId, LeaveKind) VALUES (@id, @name)", new { id, name });

    public void AddLeave(string desNo, string userId, string kind, DateTime start, DateTime end, string flow, string? dayOfWeek = null)
        => Execute("INSERT [dbo].[Form_Leave] (DesNo, LeaveUserId, LeaveKind, LeaveSDate, LeaveEDate, FlowOver, DayOfWeek) VALUES (@desNo, @userId, @kind, @start, @end, @flow, @dayOfWeek)",
            new { desNo, userId, kind, start, end, flow, dayOfWeek });

    public void AddLeaveGroup(string desNo, DateTime start, DateTime end, string dayOfWeek)
        => Execute("INSERT [dbo].[Form_Leave_Group] (DesNo, LeaveSDate, LeaveEDate, DayOfWeek) VALUES (@desNo, @start, @end, @dayOfWeek)", new { desNo, start, end, dayOfWeek });

    public void AddTravel(string desNo, string userId, DateTime start, DateTime end, string main, string flow)
        => Execute("INSERT [dbo].[Form_Travel] (DesNo, ChargeName, TravelSDate, TravelEDate, TravelMain, FlowOver) VALUES (@desNo, @userId, @start, @end, @main, @flow)",
            new { desNo, userId, start, end, main, flow });

    // GdAccess 經 SqlDatabase 用另一個連線字串連進來，那個連線池也要清掉，否則刪除資料庫時連線仍被占用。
    public void Dispose()
    {
        using (var connection = new SqlConnection(Databases.ConnectionString(GdDatabase.Name))) SqlConnection.ClearPool(connection);
        using (var connection = new SqlConnection(Writable.ConnectionString())) SqlConnection.ClearPool(connection);
        database.Dispose();
    }

    private void Execute(string sql, object? parameters = null)
    {
        using var connection = new SqlConnection(database.ConnectionString);
        connection.Execute(sql, parameters);
    }

    private static DatabasesOptions OptionsFor(string name) => new() { [GdDatabase.Name] = Connection(name, readOnly: true) };

    private static DatabaseOptions Connection(string name, bool readOnly)
        => new() { Server = TestDatabase.Server, Name = name, IntegratedSecurity = true, TrustServerCertificate = true, ConnectTimeoutSeconds = 5, ReadOnly = readOnly };
}

/// <summary>假 GD 裡一天的原始值。</summary>
internal sealed record GdDay(bool MorningOff, bool AfternoonOff, string? Remark, string UserId);
