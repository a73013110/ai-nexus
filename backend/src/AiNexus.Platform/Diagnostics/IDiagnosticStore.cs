namespace AiNexus.Platform.Diagnostics;

public interface IDiagnosticStore
{
    Task WriteAsync(IReadOnlyList<DiagnosticEvent> events, CancellationToken ct);
    Task CleanupAsync(CancellationToken ct);
}
