using AiNexus.Features.Persistence;
using AiNexus.Platform.Data;
using AiNexus.Platform.Errors;

namespace AiNexus.Host.Commands;

/// <summary>
/// Command line of the host. Without a command it serves HTTP; a command (<c>db init</c>, <c>verify …</c>) runs once
/// against the fully built host, sets the exit code and exits. Parsed before the web host is created, so an unknown
/// command or <c>--help</c> never starts the site.
/// </summary>
public sealed class HostCommand
{
    public const int UsageError = 2;

    private sealed record Definition(string Name, string Summary, bool WritesReport, Func<WebApplication, string?, Task<bool>> Run);

    private static readonly Definition[] Definitions =
    [
        new("db init", "建立資料庫（不存在時）並套用 migration；不刪除或清空既有資料", false, (app, _) => InScopeAsync(app, async services =>
        {
            await services.GetRequiredService<DatabaseInitializer>().InitializeAsync(CancellationToken.None);
            Console.WriteLine("AiNexus 資料庫與 migrations 初始化完成。");
        })),
        new("verify deployment", "唯讀檢查 SQL、設定、模型清單、embedding／rerank 與站外目錄寫入，不產生 AI 回答", false,
            (app, _) => DeploymentVerifier.VerifyAsync(app.Services, app.Configuration, app.Environment, CancellationToken.None)),
        new("verify connections", "以合成資料實測 SQL 讀寫、AD、模型串流、附件辨識與檢索模型（會使用模型配額）", true, async (app, output) =>
        {
            using var scope = app.Services.CreateScope();
            return await ConnectionVerifier.VerifyAsync(scope.ServiceProvider, output ?? Path.Combine(app.Environment.ContentRootPath, "connection-checks.json"), CancellationToken.None);
        }),
        new("verify sql", "檢查 SQL Server 版本、原生向量與全文檢索能力", true, (app, output) => InScopeAsync(app, services =>
            services.GetRequiredService<SqlVectorCapabilities>().VerifyAsync(output ?? "sql-capabilities.json", CancellationToken.None))),
    ];

    private HostCommand(string? name, string[] hostArguments, string? output, bool help, string? error)
    {
        Name = name;
        HostArguments = hostArguments;
        Output = output;
        Help = help;
        Error = error;
    }

    /// <summary>The command, or <see langword="null"/> to serve HTTP.</summary>
    public string? Name { get; }

    /// <summary>Arguments left for host configuration (<c>--LocalConfigPath</c>, <c>--urls</c>, …).</summary>
    public string[] HostArguments { get; }

    /// <summary>Report file of a <c>verify</c> command (<c>--output</c>).</summary>
    public string? Output { get; }

    public bool Help { get; }

    public string? Error { get; }

    public static string Usage { get; } = $"""
        用法：AiNexus.Host [指令] [--output <檔案>] [--<設定鍵> <值> ...]

        不帶指令時啟動網站。指令執行一次後結束：結束碼 0 成功、1 失敗、{UsageError} 用法錯誤。
        其餘 --<設定鍵> 照常覆寫設定，例如 --contentRoot、--LocalConfigPath、--SecretsConfigPath。

        指令：
        {string.Join(Environment.NewLine, Definitions.Select(x => $"  {x.Name,-20}{x.Summary}{(x.WritesReport ? "；--output 指定報告檔" : "")}"))}
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

        string? output = null;
        var index = options.FindIndex(x => x == "--output" || x.StartsWith("--output=", StringComparison.Ordinal));
        if (index >= 0)
        {
            if (!definition.WritesReport) return new(null, [], null, help: false, error: $"{name} 不接受 --output。");
            if (options[index].StartsWith("--output=", StringComparison.Ordinal))
            {
                output = options[index]["--output=".Length..];
                options.RemoveAt(index);
            }
            else if (index + 1 < options.Count && !options[index + 1].StartsWith('-'))
            {
                output = options[index + 1];
                options.RemoveRange(index, 2);
            }
            if (string.IsNullOrWhiteSpace(output)) return new(null, [], null, help: false, error: "--output 需要檔案路徑。");
        }
        return new(name, [.. options], output, help: false, error: null);
    }

    /// <summary>Runs the command and disposes the host; returns the process exit code.</summary>
    public async Task<int> RunAsync(WebApplication app)
    {
        var definition = Definitions.Single(x => x.Name == Name);
        var passed = await definition.Run(app, Output);
        await app.DisposeAsync();
        return passed ? 0 : 1;
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
            Console.Error.WriteLine(ex is ExternalServiceException external ? external.Message : LocalDatabaseSettings.Diagnose(ex));
            return false;
        }
    }
}
