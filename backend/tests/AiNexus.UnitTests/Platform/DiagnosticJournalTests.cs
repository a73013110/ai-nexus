using AiNexus.Platform.Diagnostics;
using AiNexus.UnitTests.Support;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiNexus.UnitTests.Platform;

public sealed class DiagnosticJournalTests
{
    [Fact]
    public async Task DisposedJournalCannotReopenItsCapacityOrOwnerHandles()
    {
        var directory = Path.Combine(Path.GetTempPath(), "nexus-disposed-" + Guid.NewGuid().ToString("N")); using var health = new DiagnosticHealth();
        var journal = new DiagnosticJournal(Options.Create(new DiagnosticOptions { Directory = directory }), health, new EnvironmentFixture());
        try { await journal.AppendAsync([new DiagnosticEvent()], CancellationToken.None); journal.Dispose();
            using (var lease = new FileStream(Path.Combine(directory, "capacity.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None)) Assert.True(lease.CanWrite);
            await Assert.ThrowsAsync<ObjectDisposedException>(() => journal.AppendAsync([new DiagnosticEvent()], CancellationToken.None));
        } finally { journal.Dispose(); Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task RotationBoundsUnicodeRecordsAndSharedCapacityAcrossLiveInstances()
    {
        var directory = Path.Combine(Path.GetTempPath(), "nexus-unicode-" + Guid.NewGuid().ToString("N")); using var health = new DiagnosticHealth(); using var otherHealth = new DiagnosticHealth();
        var options = Options.Create(new DiagnosticOptions { Directory = directory, FileSizeBytes = 65536, MaxDiskBytes = 180000 });
        try {
            using var first = new DiagnosticJournal(options, health, new EnvironmentFixture()); using var second = new DiagnosticJournal(options, otherHealth, new EnvironmentFixture());
            var detail = string.Join('\n', Enumerable.Repeat(new string('測', 200), 60));
            var rows = Enumerable.Range(0, 3).Select(_ => new DiagnosticEvent { Level = LogLevel.Error, MessageTemplate = new string('中', 2048), ExceptionDetail = detail, IssueCode = Issues.NewCode() }).ToArray();
            await first.AppendAsync(rows, CancellationToken.None); await second.AppendAsync([new() { Level = LogLevel.Error }], CancellationToken.None);
            Assert.All(Directory.EnumerateFiles(directory, "*.jsonl", SearchOption.AllDirectories), f => Assert.True(new FileInfo(f).Length <= 65536));
            await Assert.ThrowsAnyAsync<IOException>(() => second.AppendAsync(rows.Select(_ => new DiagnosticEvent { Level = LogLevel.Error, ExceptionDetail = detail }).ToArray(), CancellationToken.None));
            var store = new MemoryStore(); await first.ReplayAsync(store, CancellationToken.None); Assert.Equal(3, store.Events.Count); // Other live owner is not stolen.
            await second.ReplayAsync(store, CancellationToken.None); Assert.Equal(4, store.Events.Count); Assert.Equal(0, otherHealth.Corrupt);
        } finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task JournalSurvivesStoreOutageAndRestartAndDoesNotDuplicateAfterLostCheckpoint()
    {
        var directory = Path.Combine(Path.GetTempPath(), "nexus-diagnostics-" + Guid.NewGuid().ToString("N"));
        using var health = new DiagnosticHealth(); var options = Options.Create(new DiagnosticOptions { Directory = directory, BatchSize = 2 });
        var store = new MemoryStore { Offline = true }; var records = Enumerable.Range(0, 5).Select(_ => new DiagnosticEvent { Level = LogLevel.Error, IssueCode = Issues.NewCode() }).ToArray();
        try
        {
            using (var journal = new DiagnosticJournal(options, health, new EnvironmentFixture())) {
                await journal.AppendAsync(records, CancellationToken.None); await Assert.ThrowsAsync<IOException>(() => journal.ReplayAsync(store, CancellationToken.None));
                Assert.Equal(5, health.Written); Assert.Empty(store.Events);
            }
            store.Offline = false;
            using (var recovered = new DiagnosticJournal(options, health, new EnvironmentFixture())) { await recovered.ReplayAsync(store, CancellationToken.None); }
            Assert.Equal(5, store.Events.Count); Assert.All(records, x => Assert.Contains(x.LogId, store.Events.Keys));
            foreach (var file in Directory.EnumerateFiles(directory, "*.cursor", SearchOption.AllDirectories)) File.Delete(file);
            using (var replay = new DiagnosticJournal(options, health, new EnvironmentFixture())) { await replay.ReplayAsync(store, CancellationToken.None); }
            Assert.Equal(5, store.Events.Count); Assert.Equal(0, health.PendingBytes);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task CorruptAndTruncatedJournalRecordsAreCountedAndValidDataContinues()
    {
        var directory = Path.Combine(Path.GetTempPath(), "nexus-corrupt-" + Guid.NewGuid().ToString("N"));
        using var health = new DiagnosticHealth(); var options = Options.Create(new DiagnosticOptions { Directory = directory }); var store = new MemoryStore();
        try {
            using (var journal = new DiagnosticJournal(options, health, new EnvironmentFixture())) { await journal.AppendAsync([new() { Level = LogLevel.Error }], CancellationToken.None); }
            var file = Directory.EnumerateFiles(directory, "*.jsonl", SearchOption.AllDirectories).Single(); await File.AppendAllTextAsync(file, "{corrupt}\n{unfinished");
            using (var replay = new DiagnosticJournal(options, health, new EnvironmentFixture())) { await replay.ReplayAsync(store, CancellationToken.None); }
            Assert.Single(store.Events); Assert.Equal(2, health.Corrupt); Assert.Equal(2, health.Lost); Assert.Equal(0, health.PendingBytes);
        } finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task RetentionPreservesUnacknowledgedFilesButRemovesOldCommittedSegments()
    {
        var directory = Path.Combine(Path.GetTempPath(), "nexus-retention-" + Guid.NewGuid().ToString("N")); using var health = new DiagnosticHealth();
        var options = Options.Create(new DiagnosticOptions { Directory = directory, FileRetentionDays = 1 }); var store = new MemoryStore();
        try {
            using (var journal = new DiagnosticJournal(options, health, new EnvironmentFixture())) { await journal.AppendAsync([new() { Level = LogLevel.Error }], CancellationToken.None); }
            var file = Directory.EnumerateFiles(directory, "*.jsonl", SearchOption.AllDirectories).Single(); File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddDays(-3));
            using var replay = new DiagnosticJournal(options, health, new EnvironmentFixture()); replay.Cleanup(); Assert.True(File.Exists(file));
            await replay.ReplayAsync(store, CancellationToken.None); replay.Cleanup(); Assert.False(File.Exists(file));
        } finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task InvalidPathAndCapacityCannotSilentlySucceed()
    {
        using var health = new DiagnosticHealth(); var environment = new EnvironmentFixture();
        Assert.Throws<InvalidOperationException>(() => DiagnosticJournal.Resolve("relative/logs", environment));
        Assert.Throws<InvalidOperationException>(() => DiagnosticJournal.Resolve(environment.ContentRootPath, environment));
        var path = Path.Combine(Path.GetTempPath(), "nexus-no-directory-" + Guid.NewGuid().ToString("N")); await File.WriteAllTextAsync(path, "locked path");
        try { using var journal = new DiagnosticJournal(Options.Create(new DiagnosticOptions { Directory = path }), health, environment); await Assert.ThrowsAnyAsync<IOException>(() => journal.AppendAsync([new() { Level = LogLevel.Error }], CancellationToken.None)); }
        finally { File.Delete(path); }
        var directory = Path.Combine(Path.GetTempPath(), "nexus-capacity-" + Guid.NewGuid().ToString("N"));
        try { using var journal = new DiagnosticJournal(Options.Create(new DiagnosticOptions { Directory = directory, MaxDiskBytes = 1 }), health, environment); await Assert.ThrowsAnyAsync<IOException>(() => journal.AppendAsync([new() { Level = LogLevel.Error }], CancellationToken.None)); }
        finally { Directory.Delete(directory, true); }
    }
}
