using AiNexus.Data.App.Attendance;
using AiNexus.Features.Office.Attendance.Imports;

namespace AiNexus.UnitTests.Attendance;

public sealed class PunchImportPlannerTests
{
    private static readonly DateTimeOffset Modified = new(2026, 10, 1, 23, 52, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 0, 30, 0, TimeSpan.Zero);
    private static readonly TimeSpan Backoff = TimeSpan.FromMinutes(15);

    private static PunchFile Imported(long size = 100) => new() { FileName = "20261001.txt", ImportedModifiedAt = Modified, ImportedSize = size };

    private static PunchImport Attempt(string status, DateTimeOffset? finished = null, long size = 100)
        => new() { Status = status, SourceModifiedAt = Modified, SourceSize = size, FinishedAt = finished ?? Now };

    [Fact]
    public void AFileNeverSeenBeforeIsImported() => Assert.True(PunchImportPlanner.NeedsImport(null, null, Modified, 100, Now, Backoff));

    [Fact]
    public void AnUnchangedFileIsLeftAlone()
        => Assert.False(PunchImportPlanner.NeedsImport(Imported(), Attempt(PunchImportStatuses.Succeeded), Modified, 100, Now, Backoff));

    [Fact]
    public void ADifferentModifiedTimeOrSizeTriggersAReimport()
    {
        Assert.True(PunchImportPlanner.NeedsImport(Imported(), null, Modified.AddSeconds(1), 100, Now, Backoff));
        Assert.True(PunchImportPlanner.NeedsImport(Imported(), null, Modified, 101, Now, Backoff));
    }

    [Fact]
    public void AClearedVersionMeansTheFileWasMarkedForReimport()
    {
        var marked = new PunchFile { FileName = "20261001.txt" };

        Assert.True(PunchImportPlanner.NeedsImport(marked, Attempt(PunchImportStatuses.Succeeded), Modified, 100, Now, Backoff));
    }

    [Fact]
    public void AFailedFileWaitsForTheBackoffUnlessItChangedSinceTheFailure()
    {
        var failed = Attempt(PunchImportStatuses.Failed, finished: Now.AddMinutes(-5));

        Assert.False(PunchImportPlanner.NeedsImport(Imported(), failed, Modified, 100, Now, Backoff));
        Assert.True(PunchImportPlanner.NeedsImport(Imported(), failed, Modified, 100, Now.AddMinutes(10), Backoff));
        Assert.True(PunchImportPlanner.NeedsImport(Imported(), failed, Modified.AddSeconds(1), 100, Now, Backoff));
        Assert.True(PunchImportPlanner.NeedsImport(Imported(), failed, Modified, 101, Now, Backoff));
        Assert.True(PunchImportPlanner.NeedsImport(Imported(), failed, Modified, 100, Now, TimeSpan.Zero));
    }

    [Fact]
    public void AFileModifiedJustNowIsStillSettlingButAFutureTimestampIsNot()
    {
        var settle = TimeSpan.FromSeconds(30);

        Assert.True(PunchImportPlanner.IsSettling(Now.AddSeconds(-5), Now, settle));
        Assert.False(PunchImportPlanner.IsSettling(Now.AddSeconds(-30), Now, settle));
        Assert.False(PunchImportPlanner.IsSettling(Now.AddMinutes(3), Now, settle));
        Assert.False(PunchImportPlanner.IsSettling(Now.AddSeconds(-1), Now, TimeSpan.Zero));
    }
}
