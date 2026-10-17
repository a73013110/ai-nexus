namespace AiNexus.Platform.Diagnostics;

/// <summary>
/// 診斷日誌抑制範圍管理器。
/// 利用 AsyncLocal 在特定非同步執行內容中抑制重複產生的診斷日誌，避免回饋循環（Feedback Loop）。
/// </summary>
public static class DiagnosticSuppression
{
    private static readonly AsyncLocal<int> Depth = new();

    public static bool Active => Depth.Value > 0;

    /// <summary>
    /// 進入抑制日誌範圍，釋放回傳的 IDisposable 即可退出範圍。
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
