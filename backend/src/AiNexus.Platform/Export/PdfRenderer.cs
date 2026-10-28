using AiNexus.Platform.Errors;
using Microsoft.Extensions.Options;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using PrintOptions = PuppeteerSharp.PdfOptions;

namespace AiNexus.Platform.Export;

/// <summary>
/// 以主機上的 Edge／Chrome（無頭模式）把 <see cref="PrintPage"/> 產生的 HTML 印成 A4 PDF。
/// 經 PuppeteerSharp 直接走 DevTools 協定，不需要 Node 驅動，發布套件不帶任何瀏覽器或執行環境。
/// </summary>
/// <remarks>
/// 瀏覽器整個程序共用一個，第一次匯出才啟動、斷線後下次重新啟動；每次匯出用獨立的 context，用完即關。
/// 頁面關閉 JavaScript 並攔截全部網路請求，文件內容無法對外連線。
/// </remarks>
public sealed class PdfRenderer(IOptions<PdfOptions> options) : IAsyncDisposable
{
    public const string ContentType = "application/pdf";

    private readonly SemaphoreSlim concurrency = new(2, 2);
    private readonly SemaphoreSlim startup = new(1, 1);
    private IBrowser? browser;

    public async Task<byte[]> RenderAsync(string html, CancellationToken ct)
    {
        // 併發上限為 2；超過時立即拒絕，不排隊等待，避免 PDF 請求堆積佔住伺服器。
        if (!await concurrency.WaitAsync(0, ct))
            throw new ExternalServiceException(Error.RateLimited("pdf_export_busy"), "PDF 匯出正在忙碌，請稍後重試。");
        var timeout = options.Value.TimeoutSeconds * 1000;
        IBrowserContext? context = null;
        try
        {
            context = await (await BrowserAsync(timeout, ct)).CreateBrowserContextAsync();
            var page = await context.NewPageAsync();
            page.DefaultTimeout = timeout;
            page.DefaultNavigationTimeout = timeout;
            await page.SetJavaScriptEnabledAsync(false);
            await page.SetRequestInterceptionAsync(true);
            page.Request += async (_, e) =>
            {
                try
                {
                    await e.Request.AbortAsync();
                }
                catch (PuppeteerException)
                {
                    // 頁面已關閉時請求不必再處理。
                }
            };
            ct.ThrowIfCancellationRequested();
            await page.SetContentAsync(html, new SetContentOptions { WaitUntil = [WaitUntilNavigation.DOMContentLoaded] });
            return await page.PdfDataAsync(new PrintOptions
                {
                    Format = PaperFormat.A4,
                    PrintBackground = true,
                    PreferCSSPageSize = true,
                    DisplayHeaderFooter = true,
                    HeaderTemplate = "<span></span>",
                    FooterTemplate = "<div style=\"font:9px sans-serif;width:100%;text-align:center;color:#65706a\">"
                        + "<span class=\"pageNumber\"></span> / <span class=\"totalPages\"></span></div>",
                })
                .WaitAsync(TimeSpan.FromMilliseconds(timeout), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error) when (error is PuppeteerException or TimeoutException)
        {
            throw Unavailable(error);
        }
        finally
        {
            try
            {
                if (context is not null) await context.CloseAsync();
            }
            catch (PuppeteerException)
            {
                // 關閉 context 失敗不影響已產出的 PDF；context 只用一次，不會被重用。
            }
            finally
            {
                concurrency.Release();
            }
        }
    }

    private async Task<IBrowser> BrowserAsync(int timeout, CancellationToken ct)
    {
        // 瀏覽器啟動只能一次一個進行，且斷線後要重新啟動。
        await startup.WaitAsync(ct);
        try
        {
            if (browser is { IsConnected: true }) return browser;
            if (browser is not null) await CloseAsync(browser);
            var executable = options.Value.ResolveExecutable() ?? throw Unavailable(null);
            browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                ExecutablePath = executable,
                Headless = true,
                Timeout = timeout,
                // IIS 集區身分通常無法建立 Chromium 的沙箱；頁面已關閉 JavaScript 與網路，只渲染系統自己產生的 HTML。
                Args = ["--no-sandbox"],
            });
            return browser;
        }
        finally
        {
            startup.Release();
        }
    }

    private static ExternalServiceException Unavailable(Exception? error) => new(
        Error.Unavailable("pdf_renderer_unavailable"),
        "PDF 匯出暫時無法使用，請由管理員檢查匯出瀏覽器安裝與執行權限；可先匯出 Word。",
        error);

    private static async Task CloseAsync(IBrowser target)
    {
        try
        {
            await target.CloseAsync();
        }
        catch (PuppeteerException)
        {
            // 已斷線的瀏覽器關不掉時只能放棄；下一個程序照常啟動。
        }
        await target.DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (browser is not null) await CloseAsync(browser);
        concurrency.Dispose();
        startup.Dispose();
    }
}
