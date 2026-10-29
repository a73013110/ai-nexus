using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AiNexus.Data.App;
using AiNexus.Data.App.Releases;
using AiNexus.Features.Admin.Releases;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AiNexus.IntegrationTests.Support;

/// <summary>版本模組的測試資料：打包過的程式版本、部署紀錄，以及在測試庫上模擬 update.sql 寫入快照與之後的手動異動。</summary>
internal static class ReleaseFixtures
{
    public const string Revision = "0123456789abcdef0123456789abcdef01234567";

    /// <summary>像 <c>Release.ps1</c> 打包的程式：有 <c>version.json</c>。</summary>
    public static AppRelease Packaged(string version, string? minimumDatabaseVersion = null, params ReleaseNoteDto[] changes)
        => new($"{version}+{Revision}", Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            version,
            revision = Revision,
            builtAt = "2026-11-02T08:00:00+00:00",
            minimumDatabaseVersion,
            changes,
        }, JsonSerializerOptions.Web)));

    public static async Task DeployDatabaseAsync(AppFactory factory, string version)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Add(new Deployment { Component = DeploymentComponents.Database, Version = version, Revision = Revision, DeployedAt = DateTimeOffset.UtcNow, Host = "SQL-HOST" });
        await db.SaveChangesAsync();
    }

    /// <summary>update.sql 最後一步做的事：把目前的結構寫成這一版的預期快照。</summary>
    public static Task SnapshotAsync(AppFactory factory) => ExecuteAsync(factory, """
        DELETE FROM [releases].[SchemaObjects];
        INSERT INTO [releases].[SchemaObjects] ([SchemaName], [Name], [Type], [Hash], [Definition])
        SELECT [SchemaName], [Name], [Type], [Hash], [Definition] FROM [releases].[udf_SchemaObjects]();
        """);

    /// <summary>在正式機直接改 procedure：加一行註解。</summary>
    public static async Task HotfixProcedureAsync(AppFactory factory, string schema, string name)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var definition = await db.Database.SqlQuery<string>($"SELECT [definition] AS [Value] FROM sys.sql_modules WHERE [object_id] = OBJECT_ID({schema + "." + name})").SingleAsync();
        // 檔頭的「Create date」也有 CREATE，所以要連 PROCEDURE 一起比對。
        var altered = new Regex(@"\bCREATE\s+PROCEDURE\b", RegexOptions.IgnoreCase).Replace(definition, "ALTER PROCEDURE", 1);
        await db.Database.ExecuteSqlRawAsync(altered.Replace("SET NOCOUNT ON;", "SET NOCOUNT ON;\n    -- 學校端修正", StringComparison.Ordinal));
    }

    public static async Task ExecuteAsync(AppFactory factory, string sql)
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.ExecuteSqlRawAsync(sql);
    }
}
