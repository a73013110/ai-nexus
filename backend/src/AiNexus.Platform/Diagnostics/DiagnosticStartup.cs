using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace AiNexus.Platform.Diagnostics;

/// <summary>
/// 應用程式啟動引導失敗記錄器。
/// 處理在託管背景工作（Hosted Worker）與相依性注入容器完全建立前所爆發的致命錯誤。
/// 僅將固定格式中繼資料寫入緊急磁碟日誌期刊與本機主控台。
/// </summary>
public static class DiagnosticStartup
{
    /// <summary>
    /// 於主機啟動崩潰時記錄致命事件，產生 NX- 代碼並嘗試寫入期刊。
    /// </summary>
    public static async Task RecordAsync(Exception exception, IConfiguration configuration, IHostEnvironment environment)
    {
        var issue = Issues.NewCode();
        using var health = new DiagnosticHealth();
        var options = DiagnosticOptions.Read(configuration);

        try
        {
            _ = DiagnosticJournal.Resolve(options.Directory, environment);
        }
        catch (Exception)
        {
            options.Directory = "";
        }

        try
        {
            using var journal = new DiagnosticJournal(Options.Create(options), health, environment);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await journal.AppendAsync([new DiagnosticEvent
            {
                At = TimeProvider.System.GetUtcNow(),
                Level = LogLevel.Critical,
                Category = "AiNexus.Startup",
                EventId = DiagnosticEvents.Configuration,
                EventName = DiagnosticEvents.ConfigurationName,
                MessageTemplate = "Service failed to initialize; inspect masked diagnostic classification.",
                Service = options.ServiceName,
                Environment = environment.EnvironmentName,
                Version = DiagnosticLoggerProvider.Version,
                Instance = System.Environment.MachineName + "-" + System.Environment.ProcessId,
                TraceId = ActivityTraceId.CreateRandom().ToHexString(),
                SpanId = ActivitySpanId.CreateRandom().ToHexString(),
                IssueCode = issue,
                ErrorCode = "startup_failed",
                ExceptionType = DiagnosticRedactor.Text(exception.GetType().FullName, 180),
                ExceptionDetail = DiagnosticRedactor.Exception(exception)
            }], timeout.Token);
        }
        catch (Exception)
        {
            health.Emergency("startup_journal_failed", 1);
        }

        // 不透明查證代碼亦輸出至標準錯誤流，供本機或維運工程師在應用程式無法啟動時比對。
        try
        {
            Console.Error.WriteLine("AiNexus startup failed. Issue: " + issue);
        }
        catch (IOException)
        {
        }
    }
}
