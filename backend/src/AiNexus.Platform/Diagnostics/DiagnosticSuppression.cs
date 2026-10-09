namespace AiNexus.Platform.Diagnostics;

public static class DiagnosticSuppression
{
    private static readonly AsyncLocal<int> Depth = new();
    public static bool Active => Depth.Value > 0;
    public static IDisposable Enter() { Depth.Value++; return new Exit(); }
    private sealed class Exit : IDisposable { public void Dispose() => Depth.Value--; }
}
