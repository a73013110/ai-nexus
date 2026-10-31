using Microsoft.CodeAnalysis;

namespace AiNexus.GeneratorTests.Support;

/// <summary>一次產生器執行的結果。<paramref name="Errors"/> 是加入產出後整個編譯的錯誤（不含產生器自己回報的診斷）。</summary>
internal sealed record GeneratorRun(IReadOnlyList<Diagnostic> Diagnostics, string Source, IReadOnlyList<Diagnostic> Errors);
