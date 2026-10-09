using Xunit;

namespace AiNexus.Tests;

/// <summary>Real-browser test; runs only when <c>AINEXUS_TEST_BROWSER</c> names the browser (see <see cref="TestBrowser"/>).</summary>
public sealed class BrowserFactAttribute : FactAttribute
{
    public BrowserFactAttribute() { if (TestBrowser.Target is null) Skip = "未設定 AINEXUS_TEST_BROWSER，略過真實瀏覽器測試；請用 scripts/Test-Diagnostics.ps1 -Browser 執行。"; }
}
