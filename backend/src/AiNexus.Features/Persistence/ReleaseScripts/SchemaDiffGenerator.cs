using AiNexus.Platform.Data;
using AiNexus.Platform.Data.Sql;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Persistence.ReleaseScripts;

/// <summary>
/// What a release changes in each schema object, taken from a real SQL Server instead of the migration code: a temporary
/// database on the <c>Databases:Nexus</c> server is created the way <c>db init</c> creates AiNexus, migrated to the
/// production version and extracted per object, then migrated to the release version and extracted again, also as the
/// <see cref="ExpectedDacpacFileName"/> that the deployed database is compared with. The temporary database is always
/// dropped, also after a failure or Ctrl+C.
/// </summary>
/// <remarks>
/// Needs the same right as <c>db init</c> (CREATE DATABASE), so it runs on a development or build server, never against
/// production. The migrations are the shipped ones, so the expected schema cannot drift from what the DBA runs.
/// </remarks>
public sealed class SchemaDiffGenerator(IOptions<DatabasesOptions> databases, ISqlDatabase<NexusMasterDatabase> master)
{
    public const string TemporaryDatabasePrefix = "AiNexus_Snapshot_";
    public const string ExpectedDacpacFileName = "expected.dacpac";
    public const string DiffDirectoryName = "diff";

    // Migrations and SqlPackage on a fresh database take seconds, but InitialCreate builds every table and the full-text index.
    private const int MigrationTimeoutSeconds = 600;

    /// <param name="from">The production database's last migration (id or name), or <c>0</c> for an empty database.</param>
    /// <param name="to">The release's last migration; <see langword="null"/> for the latest.</param>
    /// <param name="output">Receives <see cref="DiffDirectoryName"/>/ and <see cref="ExpectedDacpacFileName"/>; neither may exist yet.</param>
    /// <param name="sqlPackage">Extracts the temporary database.</param>
    /// <param name="ct">Cancels the work; the temporary database is dropped all the same.</param>
    /// <exception cref="ReleaseScriptException">The range or output is invalid, or SqlPackage failed.</exception>
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
        // Resolving the range opens no connection, so a mistyped migration fails before anything is created.
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
            // Also when CREATE succeeded and the RCSI switch did not. A failed drop must not hide the error that came first.
            try { await DropAsync(name, throwOnFailure: succeeded); }
            finally { work.Delete(recursive: true); }
        }
    }

    /// <summary>Drops the temporary database whatever happened before; a database left behind is named so a DBA can drop it.</summary>
    private async Task DropAsync(string name, bool throwOnFailure)
    {
        // EF keeps pooled connections to the temporary database open; SINGLE_USER would roll them back anyway.
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
