using AiNexus.Platform.Data;
using AiNexus.Platform.Data.Sql;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Persistence.ReleaseScripts;

/// <summary>
/// 比對一次發布會改變哪些結構物件。結果來自真正的 SQL Server，而不是 migration 程式碼：
/// 在 <c>Databases:Nexus</c> 伺服器上建立暫存庫（建立方式同 <c>db init</c>），套用正式版 migration 後逐物件匯出，
/// 再套用發布版 migration 並再次匯出；另外也匯出一份 <see cref="ExpectedDacpacFileName"/>，供部署後的資料庫比對。
/// 暫存庫無論成功或失敗（包括 Ctrl+C）都會刪除。
/// </summary>
/// <remarks>
/// 需要與 <c>db init</c> 相同的權限（CREATE DATABASE），因此只能在開發或建置伺服器執行，不可對正式環境執行。
/// 使用的是實際出貨的 migration，預期結構不會與 DBA 實際執行的內容產生落差。
/// </remarks>
public sealed class SchemaDiffGenerator(IOptions<DatabasesOptions> databases, ISqlDatabase<NexusMasterDatabase> master)
{
    public const string TemporaryDatabasePrefix = "AiNexus_Snapshot_";
    public const string ExpectedDacpacFileName = "expected.dacpac";
    public const string DiffDirectoryName = "diff";

    // 新資料庫上的 migration 與 SqlPackage 通常只要幾秒，但 InitialCreate 會建立所有資料表與全文索引，所以給較長的逾時。
    private const int MigrationTimeoutSeconds = 600;

    /// <param name="from">正式資料庫目前最後套用的 migration（id 或名稱）；空庫用 <c>0</c>。</param>
    /// <param name="to">發布的最後一個 migration；<see langword="null"/> 表示最新。</param>
    /// <param name="output">輸出 <see cref="DiffDirectoryName"/> 目錄與 <see cref="ExpectedDacpacFileName"/>；兩者都不能已存在。</param>
    /// <param name="sqlPackage">用來匯出暫存庫的 SqlPackage。</param>
    /// <param name="ct">取消作業；暫存庫仍會照常刪除。</param>
    /// <exception cref="ReleaseScriptException">範圍或輸出目錄無效，或 SqlPackage 失敗。</exception>
    public async Task<SchemaDiff> GenerateAsync(string from, string? to, string output, SqlPackage sqlPackage, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sqlPackage);
        var nexus = databases.Value.For(NexusDbContext.DatabaseName);
        if (!nexus.Configured) throw new ReleaseScriptException("請先設定 Databases.Nexus 的 Server、Name 與可建庫的帳密（或 IntegratedSecurity=true）；比對會在該伺服器建立暫存庫。");
        var root = Path.GetFullPath(output);
        var dacpac = Path.Combine(root, ExpectedDacpacFileName);
        var diffDirectory = Path.Combine(root, DiffDirectoryName);
        if (File.Exists(dacpac) || Directory.Exists(diffDirectory)) throw new ReleaseScriptException($"{root} 已有 {ExpectedDacpacFileName} 或 {DiffDirectoryName}/，請用新的輸出目錄，避免混入舊檔。");

        var name = TemporaryDatabasePrefix + Guid.NewGuid().ToString("N");
        var connection = nexus.ConnectionString(name);
        // 解析範圍不會開連線，因此 migration 名稱打錯時，會在建立任何暫存庫之前就失敗。
        await using var db = new NexusDbContext(new DbContextOptionsBuilder<NexusDbContext>()
            .UseSqlServer(connection, sql => sql.CommandTimeout(MigrationTimeoutSeconds)).Options);
        var (fromId, toId, migrations) = Range(db.GetService<IMigrationsAssembly>(), from, to);
        var migrator = db.GetService<IMigrator>();

        var work = Directory.CreateTempSubdirectory("AiNexus-schema-diff-");
        var succeeded = false;
        try
        {
            Console.WriteLine($"在 {nexus.Server} 建立暫存庫 {name}，比對 {fromId ?? "0"} → {toId}（{migrations.Count} 個 migration）。");
            await DatabaseInitializer.CreateAsync(master, name, ct);
            var server = await master.QuerySingleAsync<ServerInfo>(ServerInfo.Sql, commandTimeout: 5, cancellationToken: ct);
            if (fromId is not null) await migrator.MigrateAsync(fromId, ct);
            var before = Path.Combine(work.FullName, "before");
            var after = Path.Combine(work.FullName, "after");
            await sqlPackage.ExtractAsync(connection, before, perObject: true, ct);
            await migrator.MigrateAsync(toId, ct);
            await sqlPackage.ExtractAsync(connection, after, perObject: true, ct);
            Directory.CreateDirectory(root);
            await sqlPackage.ExtractAsync(connection, dacpac, perObject: false, ct);

            var diff = new SchemaDiff(fromId, toId, migrations, server.Version, server.FullText, SchemaDiff.Compare(before, after));
            diff.WriteTo(diffDirectory);
            succeeded = true;
            return diff;
        }
        finally
        {
            // CREATE 成功但後續失敗（例如 RCSI 切換失敗）時也要刪除；刪除失敗不可蓋掉先發生的錯誤。
            try { await DropAsync(name, throwOnFailure: succeeded); }
            finally { work.Delete(recursive: true); }
        }
    }

    /// <summary>不論先前發生什麼都刪除暫存庫；若刪除失敗，訊息會列出名稱，讓 DBA 能手動處理。</summary>
    private async Task DropAsync(string name, bool throwOnFailure)
    {
        // 清空連線池，避免池中指向暫存庫的連線在資料庫刪除後被重用。SINGLE_USER 只會回滾連線，並不會清掉連線池。
        SqlConnection.ClearAllPools();
        try
        {
            await master.ExecuteAsync($"IF DB_ID(@Name) IS NOT NULL BEGIN ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]; END",
                new { Name = name }, commandTimeout: 120, cancellationToken: CancellationToken.None);
            Console.WriteLine($"已刪除暫存庫 {name}。");
        }
        catch (Exception ex) when (ex is SqlException or InvalidOperationException)
        {
            var message = $"暫存庫 {name} 刪除失敗，請 DBA 手動執行 DROP DATABASE [{name}]：{ex.Message}";
            if (throwOnFailure) throw new ReleaseScriptException(message, ex);
            Console.Error.WriteLine(message);
        }
    }

    private static (string? From, string To, IReadOnlyList<string> Migrations) Range(IMigrationsAssembly assembly, string from, string? to)
    {
        var ids = assembly.Migrations.Keys.ToList();
        if (ids.Count == 0) throw new ReleaseScriptException("沒有任何 migration。");
        var fromId = from == Migration.InitialDatabase ? null : ReleaseScriptGenerator.Resolve(assembly, from, "--from");
        if (to == Migration.InitialDatabase) throw new ReleaseScriptException("--to 不能是 0；比對的是升級後的結構。");
        var toId = to is null ? ids[^1] : ReleaseScriptGenerator.Resolve(assembly, to, "--to");
        if (fromId is not null && string.Compare(toId, fromId, StringComparison.OrdinalIgnoreCase) < 0)
            throw new ReleaseScriptException($"--to {toId} 早於 --from {fromId}；只比對升級。");
        var range = ids.Where(id => (fromId is null || string.Compare(id, fromId, StringComparison.OrdinalIgnoreCase) > 0) && string.Compare(id, toId, StringComparison.OrdinalIgnoreCase) <= 0).ToList();
        return (fromId, toId, range);
    }

    private sealed class ServerInfo
    {
        public const string Sql = """
            SELECT CONVERT(nvarchar(128), SERVERPROPERTY('ProductVersion')) AS Version,
                CAST(CASE WHEN CONVERT(int, SERVERPROPERTY('IsFullTextInstalled')) = 1 AND EXISTS (SELECT 1 FROM sys.fulltext_languages WHERE lcid = 1028) THEN 1 ELSE 0 END AS bit) AS FullText
            """;

        public string Version { get; set; } = "";
        public bool FullText { get; set; }
    }
}
