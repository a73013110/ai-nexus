using Xunit;

namespace AiNexus.Tests;

/// <summary>Test of Windows-only path semantics (drive letters, case-insensitive paths).</summary>
public sealed class WindowsFactAttribute : FactAttribute
{
    public WindowsFactAttribute() { if (!OperatingSystem.IsWindows()) Skip = "只在 Windows 驗證磁碟機路徑語意。"; }
}
