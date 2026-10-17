using System.Data.Common;
using AiNexus.Features.Persistence;
using AiNexus.Features.Persistence.ReleaseScripts;
using AiNexus.Platform.Data;
using AiNexus.Platform.Errors;

namespace AiNexus.Host.Commands;

/// <summary>
/// 主機命令列指令處理器。
/// 不帶指令時啟動 HTTP 伺服器；帶指令（<c>db init</c>、<c>db script</c>、<c>db diff</c>、<c>verify …</c>）時，
/// 會對完整建好的主機執行一次該作業，設定結束碼後退出。
/// 指令在建立 Web 主機之前就先解析，因此未知指令或 <c>--help</c> 不會意外啟動網站。
/// </summary>
public sealed class HostCommand
{
    public const int UsageError = 2;

    private sealed record CommandOption(string Name, string Value, bool Required, string Summary)
    {
        public string Usage => $"{Name} <{Value}>{(Required ? "（必填）" : "")}{Summary}";
    }

    private sealed record Definition(string Name, string Summary, CommandOption[] Options, Func<WebApplication, HostCommand, Task<bool>> Run);

    private static readonly CommandOption Report = new("--output", "檔案", false, "指定報告檔");

    private static readonly Definition[] Definitions =
    [
        new("db init", "建立資料庫（不存在時）並套用 migration；不刪除或清空既有資料", [], (app, _) => InScopeAsync(app, async services =>
        {
            var snapshot = await services.GetRequiredService<DatabaseInitializer>().InitializeAsync(CancellationToken.None);
            Console.WriteLine("AiNexus 資料庫與 migrations 初始化完成。");
            if (!snapshot)
            {
                Console.WriteLine("注意：既有資料庫尚未開啟 READ_COMMITTED_SNAPSHOT，請 DBA 依 docs/architecture/database.md 在維護時段開啟。");
            }
        })),
        new("db script", "不連線資料庫，產生發布用 SQL：依物件拆成 schema／data 編號檔與 manifest.md",
            [
                new("--from", "migration", true, "正式機目前最後一個 migration，0 代表空庫"),
                new("--to", "migration", false, "最後要套用的 migration，預設最新"),
                new("--output", "目錄", true, "輸出目錄，必須不存在或是空的"),
            ],
            (_, command) => Task.FromResult(WriteReleaseScript(command.Option("--from")!, command.Option("--to"), command.Output!))),
        new("db diff", "在 Databases:Nexus 的伺服器建暫存庫並套用 migration，以 SqlPackage 產生每個物件的差異（diff/）與部署後驗證用的 expected.dacpac；結束一定刪除暫存庫",
            [
                new("--from", "migration", true, "正式機目前最後一個 migration，0 代表空庫"),
                new("--to", "migration", false, "發布的最後一個 migration，預設最新"),
                new("--output", "目錄", true, "輸出目錄，其中不能已有 diff/ 或 expected.dacpac"),
                new("--sqlpackage", "檔案", false, "SqlPackage 執行檔或 .dll，預設在目前目錄執行 dotnet tool run sqlpackage"),
            ],
            (app, command) => WriteSchemaDiffAsync(app, command)),
        new("verify deployment", "唯讀檢查 SQL、設定、模型清單、embedding／rerank 與站外目錄寫入，不產生 AI 回答", [],
            (app, _) => DeploymentVerifier.VerifyAsync(app.Services, app.Configuration, app.Environment, CancellationToken.None)),
        new("verify connections", "以合成資料實測 SQL 讀寫、AD、模型串流、附件辨識與檢索模型（會使用模型配額）", [Report], async (app, command) =>
        {
            using var scope = app.Services.CreateScope();
            return await ConnectionVerifier.VerifyAsync(scope.ServiceProvider, command.Output ?? Path.Combine(app.Environment.ContentRootPath, "connection-checks.json"), CancellationToken.None);
        }),
        new("verify sql", "檢查 SQL Server 版本、原生向量與全文檢索能力", [Report], (app, command) => InScopeAsync(app, services =>
            services.GetRequiredService<SqlVectorCapabilities>().VerifyAsync(command.Output ?? "sql-capabilities.json", CancellationToken.None))),
    ];

    /// <summary>
    /// 所有指令選項名稱的聯集。解析時只靠這份清單辨識 --key；不在清單內的選項會被當成設定覆寫鍵，
    /// 所以每個指令接受哪些選項，必須在 Definitions 中明確宣告。
    /// </summary>
    private static readonly string[] CommandOptions = [.. Definitions.SelectMany(x => x.Options).Select(x => x.Name).Distinct()];

    private readonly Dictionary<string, string> options;

    private HostCommand(string? name, string[] hostArguments, Dictionary<string, string>? options, bool help, string? error)
    {
        Name = name;
        HostArguments = hostArguments;
        this.options = options ?? [];
        Help = help;
        Error = error;
    }

    /// <summary>指令名稱；若為 <see langword="null"/> 則代表啟動 HTTP 伺服器。</summary>
    public string? Name { get; }

    /// <summary>留給 Web 主機組態設定的引數（例如 <c>--LocalConfigPath</c>、<c>--urls</c> 等）。</summary>
    public string[] HostArguments { get; }

    /// <summary><c>verify</c> 指令的報告輸出檔，或 <c>db script</c> 與 <c>db diff</c> 的輸出目錄（由 <c>--output</c> 指定）。</summary>
    public string? Output => Option("--output");

    public bool Help { get; }

    public string? Error { get; }

    /// <summary>取得指令特定選項之值（例如 <c>--from</c>）；未提供時傳回 <see langword="null"/>。</summary>
    public string? Option(string name) => options.GetValueOrDefault(name);

    public static string Usage { get; } = $"""
        用法：AiNexus.Host [指令] [指令選項] [--<設定鍵> <值> ...]

        不帶指令時啟動網站。指令執行一次後結束：結束碼 0 成功、1 失敗、{UsageError} 用法錯誤。
        其餘 --<設定鍵> 照常覆寫設定，例如 --contentRoot、--LocalConfigPath、--SecretsConfigPath。

        指令：
        {string.Join(Environment.NewLine, Definitions.Select(x => $"  {x.Name,-20}{x.Summary}{string.Concat(x.Options.Select(o => "；" + o.Usage))}"))}
          --help              顯示這份說明
        """;

    public static HostCommand Parse(string[] args)
    {
        if (args.Any(x => x is "--help" or "-h" or "-?" or "/?")) return new(null, [], null, help: true, error: null);

        // 斷詞規則必須與命令列組態提供者一致："--key value" 成對、"--key=value" 為單一詞。
        // 其餘詞彙才是指令名稱，所以 "--contentRoot app db init" 仍會解析為 db init，而不是直接啟動網站。
        var words = new List<string>();
        var options = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith('-') && !args[i].StartsWith('/')) { words.Add(args[i]); continue; }
            options.Add(args[i]);
            if (!args[i].Contains('=') && i + 1 < args.Length) options.Add(args[++i]);
        }
        if (words.Count == 0) return new(null, args, null, help: false, error: null);

        var name = string.Join(' ', words);
        var definition = Definitions.FirstOrDefault(x => x.Name == name);
        if (definition is null) return new(null, [], null, help: false, error: $"未知的指令：{name}");

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var option in CommandOptions)
        {
            var index = options.FindIndex(x => x == option || x.StartsWith(option + "=", StringComparison.Ordinal));
            if (index < 0) continue;
            var accepted = definition.Options.FirstOrDefault(x => x.Name == option);
            if (accepted is null) return new(null, [], null, help: false, error: $"{name} 不接受 {option}。");
            string? value = null;
            if (options[index].StartsWith(option + "=", StringComparison.Ordinal))
            {
                value = options[index][(option.Length + 1)..];
                options.RemoveAt(index);
            }
            else if (index + 1 < options.Count && !options[index + 1].StartsWith('-'))
            {
                value = options[index + 1];
                options.RemoveRange(index, 2);
            }
            if (string.IsNullOrWhiteSpace(value)) return new(null, [], null, help: false, error: $"{option} 需要 <{accepted.Value}>。");
            values[option] = value;
        }
        if (definition.Options.FirstOrDefault(x => x.Required && !values.ContainsKey(x.Name)) is { } missing)
            return new(null, [], null, help: false, error: $"{name} 需要 {missing.Name} <{missing.Value}>。");
        return new(name, [.. options], values, help: false, error: null);
    }

    /// <summary>執行特定指令並於完成後釋放 Web 主機資源；傳回處理程序結束碼（0 成功、1 失敗）。</summary>
    public async Task<int> RunAsync(WebApplication app)
    {
        var definition = Definitions.Single(x => x.Name == Name);
        var passed = await definition.Run(app, this);
        await app.DisposeAsync();
        return passed ? 0 : 1;
    }

    private static bool WriteReleaseScript(string from, string? to, string output)
    {
        try
        {
            // 設計時期 DbContext 只用來產生 SQL，不帶連線認證，因此不會對外開啟真實資料庫連線。
            var factory = new DesignTimeDbContextFactory();
            var script = new ReleaseScriptGenerator(() => factory.CreateDbContext([])).Generate(from, to);
            script.WriteTo(output);
            Console.WriteLine($"已產生 {script.Migrations.Count} 個 migration 的 {script.Files.Count} 個 SQL 檔：{Path.GetFullPath(output)}；依 {ReleaseScript.ManifestFileName} 的編號執行。");
            return true;
        }
        catch (ReleaseScriptException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return false;
        }
    }

    private static async Task<bool> WriteSchemaDiffAsync(WebApplication app, HostCommand command)
    {
        // 攔截 Ctrl+C 改為取消作業，而不是直接中斷處理程序，讓暫存資料庫仍能執行 Drop 清理。
        using var cancel = new CancellationTokenSource();
        void Stop(object? sender, ConsoleCancelEventArgs e) { e.Cancel = true; cancel.Cancel(); }
        Console.CancelKeyPress += Stop;
        try
        {
            using var scope = app.Services.CreateScope();
            var diff = await scope.ServiceProvider.GetRequiredService<SchemaDiffGenerator>().GenerateAsync(
                command.Option("--from")!, command.Option("--to"), command.Output!, new SqlPackage(command.Option("--sqlpackage")), cancel.Token);
            var output = Path.GetFullPath(command.Output!);
            Console.WriteLine($"已產生 {diff.Changes.Count} 個物件的差異：{Path.Combine(output, SchemaDiffGenerator.DiffDirectoryName, SchemaDiff.ManifestFileName)}；部署後驗證用 {Path.Combine(output, SchemaDiffGenerator.ExpectedDacpacFileName)}。");
            if (!diff.FullTextAvailable) Console.WriteLine("注意：參考伺服器缺全文元件或繁中斷詞器，結構不含 KnowledgeSearch 全文索引。");
            return true;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("已取消。");
            return false;
        }
        catch (ReleaseScriptException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return false;
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException)
        {
            Console.Error.WriteLine(DatabaseDiagnosis.Describe(ex, NexusDbContext.DatabaseName));
            return false;
        }
        finally { Console.CancelKeyPress -= Stop; }
    }

    private static async Task<bool> InScopeAsync(WebApplication app, Func<IServiceProvider, Task> work)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            await work(scope.ServiceProvider);
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex is ExternalServiceException external ? external.Message : DatabaseDiagnosis.Describe(ex, NexusDbContext.DatabaseName));
            return false;
        }
    }
}
