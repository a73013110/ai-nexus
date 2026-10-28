using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace AiNexus.Platform.Export;

/// <summary>PDF 匯出所用的瀏覽器（<see cref="PdfRenderer"/>）。</summary>
public sealed class PdfOptions : IValidatableObject
{
    public const string Section = "Export:Pdf";

    /// <summary>主機上已安裝的瀏覽器：<c>msedge</c> 或 <c>chrome</c>，從標準安裝位置尋找。</summary>
    [AllowedValues("msedge", "chrome")] public string Browser { get; set; } = "msedge";

    /// <summary>Chromium 系瀏覽器執行檔的絕對路徑；裝在非標準位置時使用，設定後取代 <see cref="Browser"/>。</summary>
    public string? BrowserExecutablePath { get; set; }

    [Range(5, 120)] public int TimeoutSeconds { get; set; } = 30;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(BrowserExecutablePath) && !Path.IsPathFullyQualified(BrowserExecutablePath))
            yield return new($"{nameof(BrowserExecutablePath)} must be an absolute path.", [nameof(BrowserExecutablePath)]);
    }

    /// <summary>實際要啟動的執行檔；找不到時為 null。設定檔不檢查是否存在：沒有瀏覽器只讓 PDF 匯出失敗，不該擋住整個網站啟動。</summary>
    internal string? ResolveExecutable() => string.IsNullOrWhiteSpace(BrowserExecutablePath)
        ? InstallPaths(Browser).FirstOrDefault(File.Exists)
        : File.Exists(BrowserExecutablePath) ? BrowserExecutablePath : null;

    private static IEnumerable<string> InstallPaths(string browser)
    {
        if (OperatingSystem.IsWindows())
        {
            var (vendor, file) = browser == "chrome" ? (@"Google\Chrome", "chrome.exe") : (@"Microsoft\Edge", "msedge.exe");
            // LocalApplicationData 是個人安裝的位置；IIS 集區身分通常沒有，系統安裝在 Program Files。
            return new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.LocalApplicationData }
                .Select(Environment.GetFolderPath)
                .Where(root => root.Length > 0)
                .Select(root => Path.Combine(root, vendor, "Application", file));
        }
        if (OperatingSystem.IsMacOS())
            return [browser == "chrome" ? "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome" : "/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge"];
        return browser == "chrome" ? ["/opt/google/chrome/chrome", "/usr/bin/google-chrome"] : ["/opt/microsoft/msedge/msedge", "/usr/bin/microsoft-edge"];
    }
}

[OptionsValidator]
public sealed partial class PdfOptionsValidator : IValidateOptions<PdfOptions>;
