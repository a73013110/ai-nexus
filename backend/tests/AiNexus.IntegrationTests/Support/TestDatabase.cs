using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using AiNexus.Data.App;
using AiNexus.IntegrationTests.Host;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.IntegrationTests.Support;

/// <summary>
/// LocalDB 上的一個測試資料庫：每個測試程序以 <c>database/App</c> 的 dacpac 與 <c>Seed/</c> 建一次範本，
/// 每個資料庫複製範本的 mdf／ldf 後 attach，釋放時刪除。
/// </summary>
/// <remarks>
/// 複製檔案再 attach 約 0.15 秒（平行測試時約 0.25 秒），比每次以 SqlPackage 部署（約 20 秒）或 RESTORE（約 0.3 秒）快。
/// 範本檔依 dacpac 的 model.xml 與手寫腳本內容快取在暫存資料夾，內容不變時下次測試直接沿用；
/// dacpac 需先建置（<c>dotnet build database/App</c>，<c>scripts/Verify.ps1</c> 會先做）。
/// LocalDB 無法啟動時的排除步驟見 <c>docs/development/test-database.md</c>。
/// </remarks>
public sealed class TestDatabase : IDisposable
{
    public const string Server = @"(localdb)\MSSQLLocalDB";
    private const string HostPrefix = "AiNexus_Test_", TemplatePrefix = "AiNexus_Template_";
    private const string Collation = "Chinese_Taiwan_Stroke_CI_AS";
    private static readonly Lazy<Template> Shared = new(Template.Create);
    private static readonly string Cache = Path.Combine(Path.GetTempPath(), "ainexus-test-db");
    private static readonly List<Task> Drops = [];

    static TestDatabase() => AppDomain.CurrentDomain.ProcessExit += (_, _) => WaitForDrops();

    private TestDatabase(string name)
    {
        Name = name;
        ConnectionString = Connection(name);
    }

    public string Name { get; }
    public string ConnectionString { get; }

    /// <summary>伺服器有沒有全文元件；LocalDB 沒有，依賴全文索引的測試以此略過。</summary>
    public static bool FullTextInstalled => Shared.Value.FullText;

    /// <summary>全文測試的略過原因。</summary>
    public const string NoFullText = "LocalDB 沒有全文元件，略過依賴全文索引的測試；全文行為由部署驗收（Test-Environment.ps1 -SqlOnly）驗證。";

    public static TestDatabase Create()
    {
        var template = Shared.Value;
        var id = Guid.NewGuid().ToString("N");
        var hosts = Directory.CreateDirectory(Path.Combine(Cache, "hosts")).FullName;
        var (data, log) = (Path.Combine(hosts, id + ".mdf"), Path.Combine(hosts, id + "_log.ldf"));
        File.Copy(template.Data, data);
        File.Copy(template.Log, log);
        var database = new TestDatabase(HostPrefix + id);
        Master($"CREATE DATABASE {Quote(database.Name)} ON (FILENAME = {Literal(data)}), (FILENAME = {Literal(log)}) FOR ATTACH");
        return database;
    }

    /// <summary>不含 App 結構的空資料庫（collation 同範本），給模擬外部系統（例如公文系統 GD）的測試自己建表；釋放與過期清理同 <see cref="Create"/>。</summary>
    public static TestDatabase CreateEmpty()
    {
        var database = new TestDatabase(HostPrefix + Guid.NewGuid().ToString("N"));
        Master($"CREATE DATABASE {Quote(database.Name)} COLLATE {Collation}");
        return database;
    }

    /// <summary>在新的資料庫上以 <see cref="AppDbContext"/> 執行 <paramref name="test"/>，結束後刪除資料庫；給不需要 host 的資料庫測試。</summary>
    public static async Task WithContextAsync(Func<AppDbContext, Task> test)
    {
        using var database = Create();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(database.ConnectionString).Options);
        await test(db);
    }

    public void Dispose()
    {
        // 只清自己的連線池；ClearAllPools 會關掉平行測試正在用的連線。
        using (var connection = new SqlConnection(ConnectionString)) SqlConnection.ClearPool(connection);
        var name = Name;
        lock (Drops) Drops.Add(Task.Run(() => Drop(name)));
    }

    /// <summary>
    /// DROP 會一併刪除 attach 進來的檔案。平行測試時每次約 0.6 秒，在背景執行，不讓測試執行緒等待；
    /// 程序結束前等全部刪完。連線池已清空，通常不必先踢掉連線，仍被占用時才改單一使用者模式。
    /// </summary>
    private static void Drop(string name)
    {
        try { Master($"DROP DATABASE {Quote(name)}"); }
        catch (SqlException error) when (error.Number == 3702)
        {
            Master($"ALTER DATABASE {Quote(name)} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE {Quote(name)}");
        }
    }

    private static void WaitForDrops()
    {
        Task[] pending;
        lock (Drops) pending = [.. Drops];
        // 刪不掉的留給下一次測試的 RemoveStale。
        try { Task.WaitAll(pending, TimeSpan.FromMinutes(1)); }
        catch (AggregateException) { }
    }

    private static string Connection(string database) => new SqlConnectionStringBuilder
    {
        DataSource = Server, InitialCatalog = database, IntegratedSecurity = true, TrustServerCertificate = true,
        // LocalDB 第一次連線時才啟動執行個體，可能要十幾秒。
        ConnectTimeout = 60, ApplicationName = "AiNexus.IntegrationTests",
    }.ConnectionString;

    private static void Master(string sql)
    {
        using var connection = new SqlConnection(Connection("master"));
        connection.Execute(sql, commandTimeout: 60);
    }

    private static string Quote(string name) => "[" + name.Replace("]", "]]", StringComparison.Ordinal) + "]";
    private static string Literal(string value) => "N'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";

    /// <summary>已 detach 的範本檔；同一份內容只建一次，跨測試程序以具名 mutex 保護。</summary>
    private sealed record Template(string Data, string Log, bool FullText)
    {
        public static Template Create()
        {
            var root = OpenApiContractTests.RepositoryRoot();
            var project = Path.Combine(root, "database", "App");
            var dacpac = new[] { "Release", "Debug" }.Select(x => Path.Combine(project, "bin", x, "App.dacpac"))
                .Where(File.Exists).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault()
                ?? throw new InvalidOperationException("找不到 App.dacpac；先執行 dotnet build database/App。");
            var fullText = Probe();
            var scripts = Scripts(project, fullText);
            var key = Key(dacpac, scripts);
            var folder = Path.Combine(Cache, key);
            var template = new Template(Path.Combine(folder, "template.mdf"), Path.Combine(folder, "template_log.ldf"), fullText);

            using var mutex = new Mutex(false, @"Local\AiNexus.TestDatabaseTemplate");
            try
            {
                if (!mutex.WaitOne(TimeSpan.FromMinutes(5))) throw new TimeoutException("等待其他測試程序建立範本資料庫逾時。");
            }
            catch (AbandonedMutexException) { } // 前一個測試程序中途結束；沒有 ready 檔的範本底下會重建。
            try
            {
                RemoveStale(key);
                var ready = Path.Combine(folder, "ready");
                if (!File.Exists(ready)) template.Build(root, dacpac, scripts, TemplatePrefix + key);
                // ready 檔的時間＝最後使用時間，RemoveStale 依此判斷其他版本的範本是否還有人用。
                File.SetLastWriteTimeUtc(ready, DateTime.UtcNow);
                return template;
            }
            finally { mutex.ReleaseMutex(); }
        }

        private void Build(string root, string dacpac, string[] scripts, string name)
        {
            var folder = Path.GetDirectoryName(Data)!;
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
            Directory.CreateDirectory(folder);
            // 自己建庫才能指定檔案位置；collation 與 dacpac 相同，SqlPackage 再套用 RCSI 等資料庫選項。
            Master($"CREATE DATABASE {Quote(name)} ON (NAME = N'AiNexus', FILENAME = {Literal(Data)}) LOG ON (NAME = N'AiNexus_log', FILENAME = {Literal(Log)}) COLLATE {Collation}");
            var connection = Connection(name);
            Publish(root, dacpac, connection);
            using (var db = new SqlConnection(connection))
            {
                // 全文語句不能在交易內執行，逐批直接送出。
                foreach (var script in scripts)
                    foreach (var batch in Regex.Split(File.ReadAllText(script), @"^\s*GO\s*$", RegexOptions.Multiline).Where(x => !string.IsNullOrWhiteSpace(x)))
                        db.Execute(batch, commandTimeout: 120);
                SqlConnection.ClearPool(db);
            }
            Master($"ALTER DATABASE {Quote(name)} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; EXEC sp_detach_db {Literal(name)}");
            File.WriteAllText(Path.Combine(folder, "ready"), name);
        }

        /// <summary>以工具清單固定版本的 SqlPackage 發布 dacpac。</summary>
        private static void Publish(string root, string dacpac, string connection)
        {
            var start = new ProcessStartInfo("dotnet") { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in new[] { "tool", "run", "sqlpackage", "/Action:Publish", "/SourceFile:" + dacpac, "/TargetConnectionString:" + connection })
                start.ArgumentList.Add(argument);
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            if (process.ExitCode != 0) throw new InvalidOperationException("dacpac 部署失敗：" + output.Result + error.Result);
        }

        /// <summary>結構以外要執行的手寫腳本：有全文元件時先 <c>FullText/</c> 的目錄再索引，最後 <c>Seed/</c>。</summary>
        private static string[] Scripts(string project, bool fullText)
        {
            string[] Files(string folder) => Directory.Exists(folder) ? [.. Directory.EnumerateFiles(folder, "*.sql").Order(StringComparer.Ordinal)] : [];
            var text = fullText ? Files(Path.Combine(project, "FullText", "Catalogs")).Concat(Files(Path.Combine(project, "FullText", "Indexes"))) : [];
            return [.. text, .. Files(Path.Combine(project, "Seed"))];
        }

        /// <summary>
        /// 範本內容的雜湊：dacpac 每次 build 都會換掉時間戳，只有 model.xml 代表結構；再加上會執行的腳本。
        /// </summary>
        private static string Key(string dacpac, string[] scripts)
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            using (var archive = ZipFile.OpenRead(dacpac))
            using (var model = archive.GetEntry("model.xml")!.Open())
            {
                var buffer = new byte[81920];
                for (var read = model.Read(buffer); read > 0; read = model.Read(buffer)) hash.AppendData(buffer, 0, read);
            }
            foreach (var script in scripts) hash.AppendData(File.ReadAllBytes(script));
            return Convert.ToHexStringLower(hash.GetHashAndReset())[..16];
        }

        private static bool Probe()
        {
            try
            {
                using var connection = new SqlConnection(Connection("master"));
                var server = connection.QuerySingle<(int Major, int FullText)>(
                    "SELECT CONVERT(int, SERVERPROPERTY('ProductMajorVersion')), CONVERT(int, SERVERPROPERTY('IsFullTextInstalled'))");
                // dacpac 的相容層級 170 只有 SQL Server 2025 支援；舊版 LocalDB（例如 Visual Studio 附的 2022 版）部署時只會給出難懂的錯誤。
                if (server.Major < 17)
                    throw new InvalidOperationException($"{Server} 是 SQL Server {server.Major} 版，需要 2025（17.x）。依 docs/development/test-database.md 的「LocalDB 無法啟動」重建執行個體。");
                return server.FullText == 1;
            }
            catch (SqlException error)
            {
                throw new InvalidOperationException($"無法連線 {Server}。依 docs/development/test-database.md 的「LocalDB 無法啟動」排除後再執行測試。", error);
            }
        }

        /// <summary>
        /// 清掉中途結束的測試程序留下的資料庫與檔案：6 小時以前的 host 資料庫、所有未 detach 的範本，以及其他版本的範本檔。
        /// 只在持有 mutex 時呼叫，正在建的範本不會被刪；同時執行中的其他測試程序的 host 資料庫未滿 6 小時，不受影響。
        /// </summary>
        private static void RemoveStale(string key)
        {
            using (var master = new SqlConnection(Connection("master")))
            {
                var stale = master.Query<string>(
                    "SELECT [name] FROM sys.databases WHERE ([name] LIKE @Host AND create_date < DATEADD(HOUR, -6, GETDATE())) OR [name] LIKE @Template",
                    new { Host = HostPrefix.Replace("_", "[_]", StringComparison.Ordinal) + "%", Template = TemplatePrefix.Replace("_", "[_]", StringComparison.Ordinal) + "%" });
                foreach (var name in stale)
                    master.Execute($"ALTER DATABASE {Quote(name)} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE {Quote(name)}", commandTimeout: 60);
            }
            if (!Directory.Exists(Cache)) return;
            var cutoff = DateTime.UtcNow.AddHours(-6);
            foreach (var folder in Directory.EnumerateDirectories(Cache).Where(x => Path.GetFileName(x) is not "hosts" && Path.GetFileName(x) != key && File.GetLastWriteTimeUtc(Path.Combine(x, "ready")) < cutoff))
                TryDelete(() => Directory.Delete(folder, recursive: true));
            var hosts = Path.Combine(Cache, "hosts");
            if (Directory.Exists(hosts))
                foreach (var file in Directory.EnumerateFiles(hosts).Where(x => File.GetLastWriteTimeUtc(x) < cutoff))
                    TryDelete(() => File.Delete(file));
        }

        // 其他測試程序仍 attach 著的檔案會被鎖住，留到下次再清。
        private static void TryDelete(Action delete)
        {
            try { delete(); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
