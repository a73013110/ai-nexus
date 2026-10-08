using System.Text.Json;
using System.Text.RegularExpressions;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace AiNexus.Tests;

public sealed class DiagnosticBrowserFactAttribute : FactAttribute
{
    public DiagnosticBrowserFactAttribute() { if (Environment.GetEnvironmentVariable("NEXUS_DIAGNOSTIC_BROWSER") != "1") Skip = "Run scripts/Test-Diagnostics.ps1 -Browser with a built frontend and local Microsoft Edge."; }
}
[CollectionDefinition("Diagnostic browser", DisableParallelization = true)]
public sealed class DiagnosticBrowserCollection;

[Collection("Diagnostic browser")]
public sealed partial class DiagnosticBrowserTests
{
    [DiagnosticBrowserFact, Trait("Category", "Browser")]
    public async Task RealFrontendCopiesIssueAndAdministratorFindsMaskedCause()
    {
        var root = Path.GetFullPath("../../../../../..", AppContext.BaseDirectory);
        var webRoot = Environment.GetEnvironmentVariable("NEXUS_DIAGNOSTIC_WEBROOT") ?? Path.Combine(root, "artifacts", "verification", "wwwroot");
        Assert.True(File.Exists(Path.Combine(webRoot, "index.html")), "Build the frontend before real browser acceptance.");
        await using var factory = new NexusFactory(administrators: ["alice"], webRoot: webRoot);
        factory.Provider.Fail = true; factory.UseKestrel(0);
        using var client = await factory.SignedInAsync();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Channel = "msedge", Headless = true });
        await using var context = await browser.NewContextAsync(new() {
            BaseURL = client.BaseAddress!.ToString(), ViewportSize = new() { Width = 1440, Height = 1000 },
            ExtraHTTPHeaders = new Dictionary<string, string> { ["X-Test-User"] = "alice" }, Permissions = ["clipboard-read", "clipboard-write"]
        });
        var page = await context.NewPageAsync(); var errors = new List<string>(); page.PageError += (_, error) => errors.Add(error);
        var evidence = Path.Combine(root, "artifacts", "diagnostic-acceptance"); Directory.CreateDirectory(evidence);
        await page.GotoAsync("/chat");
        await page.GetByRole(AriaRole.Textbox, new() { Name = "傳送訊息" }).FillAsync("受控日誌驗收：觸發模型服務失敗");
        await page.GetByRole(AriaRole.Button, new() { Name = "送出訊息", Exact = true }).ClickAsync();
        var failure = page.Locator(".error-note").First;
        await Expect(failure).ToContainTextAsync("查證代碼：NX-", new() { Timeout = 15000 });
        var text = await failure.InnerTextAsync(); var issue = Code().Match(text).Value; Assert.True(Issues.ValidCode(issue));
        Assert.DoesNotContain("fixture failure", await page.Locator("body").InnerTextAsync());
        await failure.GetByRole(AriaRole.Button, new() { Name = "複製問題查證代碼" }).ClickAsync();
        Assert.Equal(issue, await page.EvaluateAsync<string>("navigator.clipboard.readText()"));
        await page.ScreenshotAsync(new() { Path = Path.Combine(evidence, "chat-safe-error.png"), FullPage = true });
        var row = await DiagnosticTests.WaitForIssue(factory, issue);
        await page.GotoAsync("/admin/logs");
        await page.GetByLabel("查證代碼", new() { Exact = true }).FillAsync(issue);
        await page.GetByRole(AriaRole.Button, new() { Name = "查詢", Exact = true }).ClickAsync();
        await Expect(page.Locator(".log-table")).ToContainTextAsync(issue);
        await page.Locator(".log-table [data-row-action]").First.ClickAsync();
        await Expect(page.Locator(".log-detail")).ToContainTextAsync("HttpRequestException");
        Assert.DoesNotContain("fixture failure", await page.Locator("body").InnerTextAsync());
        Assert.DoesNotContain("fixture-only", await page.Locator("body").InnerTextAsync());
        await page.ScreenshotAsync(new() { Path = Path.Combine(evidence, "admin-masked-detail.png"), FullPage = true });
        await page.GetByRole(AriaRole.Dialog, new() { Name = "診斷詳情", Exact = true }).ScreenshotAsync(new() { Path = Path.Combine(evidence, "detail-and-timeline.png") });
        await page.GetByRole(AriaRole.Tab, new() { Name = "例外堆疊", Exact = true }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Tabpanel)).ToContainTextAsync("HttpRequestException");
        await page.EmulateMediaAsync(new() { ColorScheme = ColorScheme.Dark });
        await page.ScreenshotAsync(new() { Path = Path.Combine(evidence, "admin-dark.png"), FullPage = true });
        await page.SetViewportSizeAsync(375, 900);
        await page.ScreenshotAsync(new() { Path = Path.Combine(evidence, "admin-mobile.png"), FullPage = true });
        Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"), "Only the data table may scroll horizontally on a narrow viewport.");
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        Assert.True(await db.AuditEvents.AnyAsync(x => x.Action == "logs.detail"));
        await page.GetByRole(AriaRole.Button, new() { Name = "關閉診斷詳情", Exact = true }).ClickAsync();
        await page.SetViewportSizeAsync(1440, 1000);
        var noIssue = Issues.NewCode();
        await page.GetByLabel("查證代碼", new() { Exact = true }).FillAsync(noIssue);
        await page.GetByRole(AriaRole.Button, new() { Name = "查詢", Exact = true }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Status).Filter(new() { HasText = "此範圍沒有日誌" })).ToBeVisibleAsync();
        var injection = new DiagnosticEvent { IssueCode = Issues.NewCode(), Level = Microsoft.Extensions.Logging.LogLevel.Warning, MessageTemplate = "<img src=x onerror=\"globalThis.__logInjected=true\">", Category = "=HYPERLINK(\"evil\")", EventName = "fixture.injection" };
        db.Add(injection); await db.SaveChangesAsync(); // Deliberately bypass producer redaction to test the UI/CSV boundary.
        await page.GetByLabel("查證代碼", new() { Exact = true }).FillAsync(injection.IssueCode!);
        await page.RouteAsync("**/api/v1/admin/logs?*", async route => { await Task.Delay(300); await route.ContinueAsync(); });
        await page.GetByRole(AriaRole.Button, new() { Name = "查詢", Exact = true }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Status, new() { Name = "", Exact = true }).Filter(new() { HasText = "正在載入日誌" })).ToBeVisibleAsync();
        await Expect(page.Locator(".log-table")).ToContainTextAsync(injection.MessageTemplate);
        await page.UnrouteAsync("**/api/v1/admin/logs?*");
        Assert.Equal(0, await page.Locator("table img").CountAsync()); Assert.False(await page.EvaluateAsync<bool>("globalThis.__logInjected === true"));
        var download = await page.RunAndWaitForDownloadAsync(() => page.GetByRole(AriaRole.Button, new() { Name = "匯出 CSV", Exact = true }).ClickAsync());
        var csvPath = Path.Combine(evidence, "safe-export.csv"); await download.SaveAsAsync(csvPath);
        Assert.Contains("'=HYPERLINK", await File.ReadAllTextAsync(csvPath));
        factory.Services.GetRequiredService<DiagnosticHealth>().Emergency("important_queue_full", 1); // Controlled health-state injection, not a physical disk outage.
        await page.GetByRole(AriaRole.Button, new() { Name = "更新狀態", Exact = true }).ClickAsync();
        await Expect(page.Locator(".log-health")).ToContainTextAsync("日誌系統降級");
        await page.RouteAsync("**/api/v1/admin/logs?*", route => route.FulfillAsync(new() { Status = 503, ContentType = "application/problem+json", Body = JsonSerializer.Serialize(new { code = "service_unavailable", title = "Password=fixture-ui-secret https://private.test/provider", issueCode = issue }) }));
        await page.GetByRole(AriaRole.Button, new() { Name = "查詢", Exact = true }).ClickAsync();
        await Expect(page.Locator(".error-banner")).ToContainTextAsync("操作未完成，請聯絡管理員");
        Assert.DoesNotContain("fixture-ui-secret", await page.Locator("body").InnerTextAsync());
        await page.ScreenshotAsync(new() { Path = Path.Combine(evidence, "query-failure-and-degraded.png"), FullPage = true });
        await page.UnrouteAsync("**/api/v1/admin/logs?*");
        await File.WriteAllTextAsync(Path.Combine(evidence, "result.json"), JsonSerializer.Serialize(new {
            measuredAtUtc = DateTimeOffset.UtcNow, issueCode = issue, logId = row.LogId, traceId = row.TraceId, runId = row.RunId,
            copied = true, masked = true, emptyState = true, loadingState = true, queryFailureSafe = true, healthDegradedState = true, xssSafe = true, csvFormulaSafe = true,
            browser = browser.Version, viewport = "1440x1000;375x900", pageErrors = errors,
            environment = "Loopback Kestrel + real built Angular + isolated SQLite + test-only identity/model provider; no production credentials or deployment."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Assert.Empty(errors);
    }
    [GeneratedRegex("NX-[0-9A-F]{32}")] private static partial Regex Code();
}
