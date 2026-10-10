using AiNexus.Platform.Diagnostics;

namespace AiNexus.UnitTests.Support;

internal sealed class MemoryStore : IDiagnosticStore
{
    public bool Offline { get; set; }
    public Dictionary<Guid, DiagnosticEvent> Events { get; } = [];
    public Task WriteAsync(IReadOnlyList<DiagnosticEvent> events, CancellationToken ct) { if (Offline) throw new IOException("SQL offline fixture"); foreach (var item in events) Events.TryAdd(item.LogId, item); return Task.CompletedTask; }
    public Task CleanupAsync(CancellationToken ct) => Task.CompletedTask;
}
