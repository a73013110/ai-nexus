using AiNexus.Data.App;
using AiNexus.Data.App.Identity;
using AiNexus.Features.Platform.Gd;
using AiNexus.Platform.Data;
using AiNexus.Platform.Data.External;
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
        CREATE TABLE [dbo].[Sys_User] (ID varchar(20) NOT NULL PRIMARY KEY, ACCOUNT varchar(50) NULL, NAME nvarchar(50) NULL, [Disable] bit NULL, TelNum varchar(20) NULL, SubTelNum varchar(10) NULL);
        CREATE TABLE [dbo].[Sys_User_DEPT] (DEP_NO int NOT NULL, ID varchar(20) NOT NULL, TitleCode varchar(10) NULL);
        CREATE TABLE [dbo].[Sys_User_Title] (TitleID varchar(10) NOT NULL PRIMARY KEY, TitleName nvarchar(50) NULL);
        CREATE TABLE [dbo].[Form_LeaveKind] (LeaveId varchar(10) NOT NULL PRIMARY KEY, LeaveKind varchar(20) NULL, isLeave bit NOT NULL DEFAULT 1, isShow bit NOT NULL DEFAULT 1);
        CREATE TABLE [dbo].[Form_Leave] (DesNo varchar(20) NOT NULL PRIMARY KEY, LeaveUserId varchar(20) NULL, LeaveDepNO int NULL, LeaveKind varchar(10) NULL,
            LeaveSDate datetime NULL, LeaveEDate datetime NULL, LeaveTime float NULL, DayOfWeek varchar(20) NULL, FlowOver varchar(20) NULL,
            SendDate datetime NULL, LeaveDesc nvarchar(2000) NULL, AttKind nvarchar(200) NULL, AttDesc nvarchar(500) NULL, AgentDepNo int NULL, AgentUserId varchar(20) NULL, BereaveType varchar(5) NULL);
        CREATE TABLE [dbo].[Form_Leave_Group] (DesNo nvarchar(20) NULL, LeaveSDate datetime NULL, LeaveEDate datetime NULL, DayOfWeek nvarchar(20) NULL);
        CREATE TABLE [dbo].[Form_Travel] (DesNo varchar(20) NOT NULL PRIMARY KEY, ChargeName varchar(50) NULL, ChargeDepName int NULL,
            TravelSDate smalldatetime NULL, TravelEDate smalldatetime NULL, TravelTime float NULL, TravelMain nvarchar(1000) NULL, Place nvarchar(100) NULL, FlowOver varchar(20) NOT NULL,
            SendDate datetime NULL, AgentDepNo int NULL, AgentUserId varchar(20) NULL, StudyMode bit NULL, StudyKind varchar(5) NULL, TravelDesc nvarchar(500) NULL, AttDesc nvarchar(500) NULL);
        CREATE TABLE [dbo].[FLOW_CURRENT] (KEY_ID varchar(20) NOT NULL, FLOW_NO varchar(10) NOT NULL, TIMERECORD varchar(20) NULL, STATUS varchar(20) NULL,
            PERSON_ID varchar(24) NULL, ORIGIN_ID varchar(100) NULL, MEMO text NULL, ADDITION varchar(255) NULL, CONSTRAINT PK_FLOW_CURRENT PRIMARY KEY (KEY_ID, FLOW_NO));
        CREATE TABLE [dbo].[FLOW_HISTORY] (KEY_ID varchar(20) NOT NULL, FLOW_NO varchar(10) NOT NULL, TIMERECORD varchar(20) NULL, STATUS varchar(20) NULL,
            PERSON_ID varchar(24) NULL, ORIGIN_ID varchar(100) NULL, MEMO text NULL, ADDITION varchar(255) NULL);
        CREATE TABLE [dbo].[FLOW_NEXT] (KEY_ID varchar(20) NOT NULL, FLOW_NO varchar(10) NOT NULL, TIMERECORD varchar(20) NULL, STATUS varchar(20) NULL,
            PERSON_ID varchar(24) NULL, ORIGIN_ID varchar(100) NULL, MEMO text NULL, ADDITION varchar(255) NULL);
        CREATE TABLE [dbo].[Frm_All_attFile] (id int IDENTITY NOT NULL, DesNo varchar(20) NULL, [Type] varchar(2) NULL, attNo varchar(50) NULL, attFileName nvarchar(200) NULL,
            attFilePath varchar(100) NULL, attMemo nvarchar(200) NULL, Owner varchar(20) NULL);
        CREATE TABLE [dbo].[Pub_Days] (hDate datetime NOT NULL, UserId varchar(20) NOT NULL, AM bit NOT NULL, PM bit NOT NULL, hReMark varchar(255) NULL);
        CREATE TABLE [dbo].[Sys_User_DEPT_BAS] (DEP_NO int NOT NULL PRIMARY KEY, DEP_NAME nvarchar(200) NULL, DEP_CHIEF varchar(20) NULL, DEP_ACHIEF varchar(100) NULL,
            DEP_EMAIL varchar(50) NULL, PARENT_NO int NULL, UnitClass varchar(10) NULL, ACCOUNT_CODE varchar(10) NULL, DEP_WORD nvarchar(100) NULL, InWord nvarchar(100) NULL,
            OrderBy int NULL, Attach int NOT NULL DEFAULT 1, CloseDesk bit NOT NULL DEFAULT 0, [Disable] bit NOT NULL DEFAULT 0, DEP_SEARCH int NULL, Agent_No int NULL);
        """;

    private readonly TestDatabase database = TestDatabase.CreateEmpty();

    private FakeGd()
    {
        Execute(Schema);
        Databases = new() { [GdDatabase.Name] = Connection(Name) };
    }

    public string Name => database.Name;

    /// <summary>
    /// 指向這個假 GD 的連線設定，與正式預設一樣可寫入（<c>ReadOnly=false</c>）；
    /// 測試可以改 <see cref="DatabaseOptions.Name"/> 模擬 GD 中途失效或恢復，改 <see cref="DatabaseOptions.ReadOnly"/> 模擬部署設為唯讀。
    /// </summary>
    public DatabasesOptions Databases { get; }

    public static FakeGd Create() => new();

    /// <summary>指向不存在的資料庫的單一連線設定，連線一定失敗。</summary>
    public static DatabaseOptions Missing() => Connection("AiNexus_Test_Missing_" + Guid.NewGuid().ToString("N"));

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
        // 沒有 AD 帳號時給本地帳號：資料表要求兩者至少有一個。
        var user = new User { Sid = "gd-" + key, DisplayName = adAccount ?? "no-account", AdAccount = adAccount, LocalAccount = adAccount is null ? "gd-" + key : null, LastSeenAt = DateTimeOffset.UtcNow };
        db.Users.Add(user);
        db.SaveChanges();
        return user.Id;
    }

    /// <summary>指向不存在的資料庫，連線一定失敗；模擬 GD 連不上。</summary>
    public static DatabasesOptions Unreachable() => new() { [GdDatabase.Name] = Missing() };

    /// <summary>沒有 <c>Databases:Gd</c> 設定。</summary>
    public static DatabasesOptions NotConfigured() => new();

    /// <summary>以正式環境同一個 <see cref="SqlDatabase{TDatabase}"/> 實作連到 <paramref name="databases"/> 的 GD 存取；<paramref name="clock"/> 決定 <see cref="ExternalDatabase{TDatabase}.Availability"/> 的時間。</summary>
    public static ExternalDatabase<GdDatabase> AccessTo(DatabasesOptions databases, ILogger<ExternalDatabase<GdDatabase>>? logger = null, TimeProvider? clock = null)
    {
        var options = Options.Create(databases);
        return new(new SqlDatabase<GdDatabase>(options, new AuditContextSource(TimeProvider.System)), options,
            clock ?? TimeProvider.System, logger ?? NullLogger<ExternalDatabase<GdDatabase>>.Instance);
    }

    public ExternalDatabase<GdDatabase> Access(ILogger<ExternalDatabase<GdDatabase>>? logger = null, TimeProvider? clock = null) => AccessTo(Databases, logger, clock);

    /// <summary>加一個公文帳號；<paramref name="disabled"/> 寫進 <c>Disable</c>，null 模擬正式資料中未填的列。</summary>
    public void AddUser(string id, string? account, bool? disabled = false, string? name = null)
        => Execute("INSERT [dbo].[Sys_User] (ID, ACCOUNT, NAME, [Disable]) VALUES (@id, @account, @name, @disabled)",
            new { id, account, disabled, name = new DbString { Value = name ?? id, IsAnsi = false, Length = 50 } });

    /// <summary>
    /// 流程表的一列：<paramref name="table"/> 是 <c>FLOW_HISTORY</c>、<c>FLOW_CURRENT</c>、<c>FLOW_NEXT</c> 或 <see cref="AddBackupTable"/> 建的備份表；
    /// <paramref name="person"/> 照 GD 的寫法是「部門_公文帳號」，<paramref name="time"/> 是 GD 寫入的字串格式。
    /// </summary>
    public void AddFlow(string table, string desNo, string flowNo, string person, string status, string? time = null, string? memo = null, string? origin = "")
        => Execute($"INSERT [dbo].[{table}] (KEY_ID, FLOW_NO, PERSON_ID, STATUS, TIMERECORD, MEMO, ORIGIN_ID) VALUES (@desNo, @flowNo, @person, @status, @time, @memo, @origin)",
            new { desNo, flowNo, person, status, time, memo, origin });

    /// <summary>建一張流程備份表（<c>FLOW_BACKUP_&lt;民國年&gt;</c>），結構同其他流程表。</summary>
    public void AddBackupTable(string year)
        => Execute($"""
            CREATE TABLE [dbo].[FLOW_BACKUP_{year}] (KEY_ID varchar(20) NOT NULL, FLOW_NO varchar(10) NOT NULL, TIMERECORD varchar(20) NULL, STATUS varchar(20) NULL,
                PERSON_ID varchar(24) NULL, ORIGIN_ID varchar(100) NULL, MEMO text NULL, ADDITION varchar(255) NULL)
            """);

    /// <summary>公文帳號在某部門的職稱（<c>Sys_User_DEPT</c>＋<c>Sys_User_Title</c>）；職稱代碼不存在時一併建立。</summary>
    public void AddMembership(int depNo, string id, string titleCode, string titleName)
        => Execute("""
            IF NOT EXISTS (SELECT 1 FROM [dbo].[Sys_User_Title] WHERE TitleID = @titleCode) INSERT [dbo].[Sys_User_Title] (TitleID, TitleName) VALUES (@titleCode, @titleName);
            INSERT [dbo].[Sys_User_DEPT] (DEP_NO, ID, TitleCode) VALUES (@depNo, @id, @titleCode);
            """, new { depNo, id, titleCode, titleName = new DbString { Value = titleName, IsAnsi = false, Length = 50 } });

    public void AddAttachment(string desNo, string storedName, string folder, string fileName, string? owner = null, string? memo = null)
        => Execute("INSERT [dbo].[Frm_All_attFile] (DesNo, [Type], attNo, attFileName, attFilePath, attMemo, Owner) VALUES (@desNo, '1', @storedName, @fileName, @folder, @memo, @owner)",
            new { desNo, storedName, folder, owner, fileName = new DbString { Value = fileName, IsAnsi = false, Length = 200 }, memo = new DbString { Value = memo, IsAnsi = false, Length = 200 } });

    /// <summary>直接改表單或帳號的欄位（不經被測程式），補上 <see cref="AddLeave"/>、<see cref="AddTravel"/> 沒有的欄位。</summary>
    public void Update(string table, string desNo, string set, object? parameters = null)
        => Execute($"UPDATE [dbo].[{table}] SET {set} WHERE {(table == "Sys_User" ? "ID" : "DesNo")} = '{desNo}'", parameters);

    /// <summary>加一個部門；<paramref name="parent"/> 照 GD 的寫法，0 表示最上層。<paramref name="deputies"/> 是 <c>DEP_ACHIEF</c> 的原始字串。</summary>
    public void AddDepartment(int no, string? name, int? parent = 0, string? chief = null, string? deputies = null, int? order = null, bool disabled = false)
        => Execute("INSERT [dbo].[Sys_User_DEPT_BAS] (DEP_NO, DEP_NAME, PARENT_NO, DEP_CHIEF, DEP_ACHIEF, OrderBy, [Disable]) VALUES (@no, @name, @parent, @chief, @deputies, @order, @disabled)",
            new { no, name = new DbString { Value = name, IsAnsi = false, Length = 200 }, parent, chief, deputies, order, disabled });

    /// <summary>直接改部門的欄位（不經被測程式），模擬 GD 端的維護。</summary>
    public void UpdateDepartment(int no, string set, object? parameters = null)
        => Execute($"UPDATE [dbo].[Sys_User_DEPT_BAS] SET {set} WHERE DEP_NO = {no}", parameters);

    public void RemoveDepartment(int no) => Execute("DELETE [dbo].[Sys_User_DEPT_BAS] WHERE DEP_NO = @no", new { no });

    /// <summary>拿掉部門表，模擬 GD 改版或帳號沒有授權：查詢會被拒（SQL 錯誤 208）。</summary>
    public void DropDepartments() => Execute("DROP TABLE [dbo].[Sys_User_DEPT_BAS]");

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

    // 被測程式用設定組出的連線字串連進來（帶不帶唯讀意圖是兩個連線池），都要清掉，否則刪除資料庫時連線仍被占用。
    public void Dispose()
    {
        foreach (var readOnly in (bool[])[false, true])
            using (var connection = new SqlConnection(Connection(Name, readOnly).ConnectionString())) SqlConnection.ClearPool(connection);
        database.Dispose();
    }

    private void Execute(string sql, object? parameters = null)
    {
        using var connection = new SqlConnection(database.ConnectionString);
        connection.Execute(sql, parameters);
    }

    private static DatabaseOptions Connection(string name, bool readOnly = false)
        => new() { Server = TestDatabase.Server, Name = name, IntegratedSecurity = true, TrustServerCertificate = true, ConnectTimeoutSeconds = 5, ReadOnly = readOnly };
}

/// <summary>假 GD 裡一天的原始值。</summary>
internal sealed record GdDay(bool MorningOff, bool AfternoonOff, string? Remark, string UserId);
