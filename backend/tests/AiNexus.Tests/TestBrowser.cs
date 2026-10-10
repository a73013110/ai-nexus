using AiNexus.Features.Artifacts;
using Microsoft.Playwright;
using Xunit;

namespace AiNexus.Tests;

/// <summary>
/// Browser for real-browser tests (<c>Category=Browser</c>), from <c>AINEXUS_TEST_BROWSER</c>: a channel
/// (<c>msedge</c>, <c>chrome</c>, <c>chromium</c>) or an absolute executable path such as <c>/opt/pw-browsers/chromium</c>.
/// </summary>
public static class TestBrowser
{
    public static string? Target => Environment.GetEnvironmentVariable("AINEXUS_TEST_BROWSER") is { Length: > 0 } value ? value : null;

    public static void SkipUnlessConfigured() => Assert.SkipWhen(Target is null, "未設定 AINEXUS_TEST_BROWSER，略過真實瀏覽器測試；請用 scripts/Verify.ps1 -Browser 執行。");

    private static string RequiredTarget => Target ?? throw new InvalidOperationException("AINEXUS_TEST_BROWSER is not set.");
    private static bool IsExecutable => Path.IsPathFullyQualified(RequiredTarget);

    public static ExportOptions ExportOptions() => IsExecutable ? new() { BrowserExecutablePath = RequiredTarget } : new() { BrowserChannel = RequiredTarget };

    public static BrowserTypeLaunchOptions LaunchOptions() => IsExecutable
        ? new() { ExecutablePath = RequiredTarget, Headless = true }
        : new() { Channel = RequiredTarget == "chromium" ? null : RequiredTarget, Headless = true };
}
