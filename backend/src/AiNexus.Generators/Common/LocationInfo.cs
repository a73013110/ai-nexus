using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace AiNexus.Generators.Common;

/// <summary>
/// 可放進 incremental 模型的原始碼位置。<see cref="Location"/> 持有語法樹，留在模型裡會讓舊的編譯結果無法回收，
/// 所以只存路徑與範圍，回報診斷時才組回 <see cref="Location"/>。
/// </summary>
internal sealed record LocationInfo
{
    private LocationInfo(string path, TextSpan span, LinePositionSpan lines)
    {
        Path = path;
        Span = span;
        Lines = lines;
    }

    public string Path { get; }

    public TextSpan Span { get; }

    public LinePositionSpan Lines { get; }

    public Location ToLocation() => Location.Create(Path, Span, Lines);

    /// <summary>符號第一個宣告的位置；只存在於中繼資料（沒有原始碼）時回傳 null。</summary>
    public static LocationInfo? From(ISymbol symbol)
        => symbol.Locations.FirstOrDefault(location => location.IsInSource) is { SourceTree: { } tree } location
            ? new LocationInfo(tree.FilePath, location.SourceSpan, location.GetLineSpan().Span)
            : null;
}
