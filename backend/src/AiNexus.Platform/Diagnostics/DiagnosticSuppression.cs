namespace AiNexus.Platform.Diagnostics;

/// <summary>
/// 診斷日誌的抑制範圍管理器。用 AsyncLocal 在特定的非同步執行內容中抑制診斷日誌，避免回饋循環（feedback loop）。
/// </summary>
public static class DiagnosticSuppression
{
    private static readonly AsyncLocal<int> Depth = new();

    public static bool Active => Depth.Value > 0;

    /// <summary>
    /// 進入抑制範圍。釋放回傳的 IDisposable 即可離開。
    /// </summary>
    public static IDisposable Enter()
    {
        Depth.Value++;
        return new Exit();
    }

    private sealed class Exit : IDisposable
    {
        public void Dispose() => Depth.Value--;
    }
}
