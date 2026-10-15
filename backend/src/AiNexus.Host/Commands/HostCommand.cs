using AiNexus.Features.Persistence;
using AiNexus.Features.Persistence.ReleaseScripts;
using AiNexus.Platform.Data;
using AiNexus.Platform.Errors;

namespace AiNexus.Host.Commands;

/// <summary>
/// Command line of the host. Without a command it serves HTTP; a command (<c>db init</c>, <c>db script</c>, <c>verify …</c>)
/// runs once against the fully built host, sets the exit code and exits. Parsed before the web host is created, so an
/// unknown command or <c>--help</c> never starts the site.
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
            if (!snapshot) Console.WriteLine("注意：既有資料庫尚未開啟 READ_COMMITTED_SNAPSHOT，請 DBA 依 docs/architecture/database.md 在維護時段開啟。");
        })),
        new("db script", "不連線資料庫，產生發布用 SQL：依物件拆成 schema／data 編號檔與 manifest.md",
            [
                new("--from", "migration", true, "正式機目前最後一個 migration，0 代表空庫"),
                new("--to", "migration", false, "最後要套用的 migration，預設最新"),
                new("--output", "目錄", true, "輸出目錄，必須不存在或是空的"),
            ],
            (_, command) => Task.FromResult(WriteReleaseScript(command.Option("--from")!, command.Option("--to"), command.Output!))),
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

    /// <summary>Option names some command takes; given to another command they would silently become configuration keys.</summary>
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

    /// <summary>The command, or <see langword="null"/> to serve HTTP.</summary>
    public string? Name { get; }

    /// <summary>Arguments left for host configuration (<c>--LocalConfigPath</c>, <c>--urls</c>, …).</summary>
    public string[] HostArguments { get; }

    /// <summary>Report file of a <c>verify</c> command, or the output directory of <c>db script</c> (<c>--output</c>).</summary>
    public string? Output => Option("--output");

    public bool Help { get; }

    public string? Error { get; }

    /// <summary>Value of a command option such as <c>--from</c>, or <see langword="null"/> when it was not given.</summary>
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

        // Same tokenizing as the command-line configuration provider: "--key value" pairs, "--key=value" singles; any other
        // word belongs to the command, so "--contentRoot app db init" is never mistaken for serving.
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

    /// <summary>Runs the command and disposes the host; returns the process exit code.</summary>
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
            // The design-time context: SQL Server's generator without credentials, so no connection is ever opened.
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
