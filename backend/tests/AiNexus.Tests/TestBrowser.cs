using AiNexus.Features.Artifacts;
using Microsoft.Playwright;

namespace AiNexus.Tests;

/// <summary>
/// Browser for <see cref="BrowserFactAttribute"/> tests, from <c>AINEXUS_TEST_BROWSER</c>: a channel
/// (<c>msedge</c>, <c>chrome</c>, <c>chromium</c>) or an absolute executable path such as <c>/opt/pw-browsers/chromium</c>.
/// </summary>
public static class TestBrowser
{
    public static string? Target => Environment.GetEnvironmentVariable("AINEXUS_TEST_BROWSER") is { Length: > 0 } value ? value : null;

    private static string RequiredTarget => Target ?? throw new InvalidOperationException("AINEXUS_TEST_BROWSER is not set.");
    private static bool IsExecutable => Path.IsPathFullyQualified(RequiredTarget);

    public static ExportOptions ExportOptions() => IsExecutable ? new() { BrowserExecutablePath = RequiredTarget } : new() { BrowserChannel = RequiredTarget };

    public static BrowserTypeLaunchOptions LaunchOptions() => IsExecutable
        ? new() { ExecutablePath = RequiredTarget, Headless = true }
        : new() { Channel = RequiredTarget == "chromium" ? null : RequiredTarget, Headless = true };
}
