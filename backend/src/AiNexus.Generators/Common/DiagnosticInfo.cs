using Microsoft.CodeAnalysis;

namespace AiNexus.Generators.Common;

/// <summary>可放進 incremental 模型的診斷：描述元是靜態的，訊息參數只用字串，位置用 <see cref="LocationInfo"/>。</summary>
internal sealed record DiagnosticInfo
{
    public DiagnosticInfo(DiagnosticDescriptor descriptor, LocationInfo? location, params string[] arguments)
    {
        Descriptor = descriptor;
        Location = location;
        Arguments = new([.. arguments]);
    }

    public DiagnosticDescriptor Descriptor { get; }

    public LocationInfo? Location { get; }

    public EquatableArray<string> Arguments { get; }

    public Diagnostic ToDiagnostic() => Diagnostic.Create(Descriptor, Location?.ToLocation(), [.. Arguments]);
}
