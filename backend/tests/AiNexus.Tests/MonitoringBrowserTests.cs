using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Xunit;
using AiNexus.Features.Persistence;
using static Microsoft.Playwright.Assertions;

namespace AiNexus.Tests;

[Collection("Diagnostic browser")]
public sealed class MonitoringBrowserTests
{
    [DiagnosticBrowserFact, Trait("Category", "Browser")]
    public async Task RealBrowserHeartbeatAndOperationReachLiveDashboard()
    {
        var root = Path.GetFullPath("../../../../../..", AppContext.BaseDirectory);
        var webRoot = Environment.GetEnvironmentVariable("NEXUS_DIAGNOSTIC_WEBROOT") ?? Path.Combine(root, "artifacts", "verification", "wwwroot");
        await using var factory = new NexusFactory(administrators: ["alice"], backgroundJobs: false, webRoot: webRoot);
        factory.UseKestrel(0); using var client = await factory.SignedInAsync();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Channel = "msedge", Headless = true });
        await using var context = await browser.NewContextAsync(new() {
            BaseURL = client.BaseAddress!.ToString(), ViewportSize = new() { Width = 1440, Height = 1000 },
            ExtraHTTPHeaders = new Dictionary<string, string> { ["X-Test-User"] = "alice" }
        });
        var page = await context.NewPageAsync(); var errors = new List<string>(); page.PageError += (_, error) => errors.Add(error);
        await page.GotoAsync("/chat");
        await page.GetByRole(AriaRole.Textbox, new() { Name = "傳送訊息" }).FillAsync("即時營運監控的受控驗收");
        await page.GetByRole(AriaRole.Button, new() { Name = "送出訊息", Exact = true }).ClickAsync();
        await Expect(page.Locator("article[aria-label='AI 回覆'][aria-busy='false']").First).ToBeVisibleAsync();
        await page.Locator(".workspace-disclosure").ClickAsync();
        await page.GetByRole(AriaRole.Link, new() { Name = "即時監控", Exact = true }).ClickAsync();
        await Expect(page.GetByText("即時連線", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(page.Locator(".monitor-table")).ToContainTextAsync("alice");
        await Expect(page.Locator(".activity-list")).ToContainTextAsync("執行對話", new() { Timeout = 15000 });
        await page.GetByRole(AriaRole.Button, new() { Name = "檢視 alice 的連線", Exact = true }).ClickAsync();
        var inspector = page.GetByRole(AriaRole.Dialog, new() { Name = "工作階段詳情", Exact = true });
        await Expect(inspector).ToContainTextAsync("Active Directory");
        await Expect(inspector).ToContainTextAsync("Edge");
        await page.GetByRole(AriaRole.Button, new() { Name = "關閉工作階段詳情", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "暫停更新", Exact = true }).ClickAsync();
        await Expect(page.GetByText("已暫停", new() { Exact = true })).ToBeVisibleAsync();
        var evidence = Path.Combine(root, "artifacts", "monitoring-acceptance"); Directory.CreateDirectory(evidence);
        await page.ScreenshotAsync(new() { Path = Path.Combine(evidence, "real-browser-live-monitor.png") });
        using var scope = factory.Services.CreateScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<NexusDbContext>().AuditEvents.AnyAsync(x => x.Action == "monitoring.view"));
        Assert.Empty(errors);
    }
}
