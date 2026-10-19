using System.Text.Json;
using AiNexus.Data.App;
using AiNexus.Platform.Data;
using AiNexus.Platform.Errors;

namespace AiNexus.Host.Commands;

/// <summary>
/// 主機命令列指令處理器。
/// 不帶指令時啟動 HTTP 伺服器；帶指令（<c>verify …</c>）時，
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
        new("verify deployment", "唯讀檢查 SQL、設定、模型清單、embedding／rerank 與站外目錄寫入，不產生 AI 回答", [],
            (app, _) => DeploymentVerifier.VerifyAsync(app.Services, app.Configuration, app.Environment, CancellationToken.None)),
        new("verify connections", "以合成資料實測 SQL 讀寫、AD、模型串流、附件辨識與檢索模型（會使用模型配額）", [Report], (app, command) =>
            ConnectionVerifier.VerifyAsync(app.Services, command.Output ?? Path.Combine(app.Environment.ContentRootPath, "connection-checks.json"), CancellationToken.None)),
        new("verify sql", "檢查 SQL Server 版本、原生向量與全文檢索能力", [Report], (app, command) => InScopeAsync(app, services =>
            services.GetRequiredService<SqlVectorCapabilities>().VerifyAsync(command.Output ?? "sql-capabilities.json", CancellationToken.None))),
    ];

    /// <summary>
    /// 所有指令選項名稱的聯集。解析時只靠這份清單辨識 --key；不在清單內的選項會被當成設定覆寫鍵，
    /// 所以每個指令接受哪些選項，必須在 Definitions 中明確宣告。
    /// </summary>
    private static readonly string[] CommandOptions = [.. Definitions.SelectMany(x => x.Options).Select(x => x.Name).Distinct()];

    /// <summary>指令輸出 JSON 報告的格式；報告給人讀，所以縮排。</summary>
    internal static JsonSerializerOptions ReportJson { get; } = new() { WriteIndented = true };

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

    /// <summary><c>verify</c> 指令的報告輸出檔（由 <c>--output</c> 指定）。</summary>
    public string? Output => Option("--output");

    public bool Help { get; }

    public string? Error { get; }

    /// <summary>取得指令特定選項之值（例如 <c>--output</c>）；未提供時傳回 <see langword="null"/>。</summary>
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
        // 其餘詞彙才是指令名稱，所以 "--contentRoot app verify sql" 仍會解析為 verify sql，而不是直接啟動網站。
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
            Console.Error.WriteLine(Describe(ex));
            return false;
        }
    }

    /// <summary>
    /// 指令失敗時給操作者看的說明：外部服務用它自己的訊息，其餘依資料庫診斷分類。
    /// 不輸出例外內容，避免連線字串或主機名稱出現在部署紀錄。
    /// </summary>
    internal static string Describe(Exception ex) =>
        ex is ExternalServiceException external ? external.Message : DatabaseDiagnosis.Describe(ex, AppDbContext.DatabaseName);
}
