using System.Text;
using AiNexus.Features.Inference;
using AiNexus.Features.Repositories;

namespace AiNexus.UnitTests.Repositories;

public sealed class RepositoryReviewPlanTests
{
    private static string Diff(int files, int size) => string.Concat(Enumerable.Range(0, files).Select(i =>
        $"diff --git a/file-{i}.cs b/file-{i}.cs\n@@ -1 +1 @@\n-old\n+{new string('x', size)}\n"));

    [Fact]
    public void UnicodeSourceAndEvidenceArePreservedWithinTheByteBudget()
    {
        var text = string.Concat(Enumerable.Repeat("+中文🙂保留整行內容\n", 300));
        var diff = "diff --git a/中文.cs b/中文.cs\n" + text;
        var slices = RepositoryReviewService.Split(diff, 512).Value!;
        Assert.Equal(diff, string.Concat(slices.Select(x => x.Diff)));
        var strict = new UTF8Encoding(false, true);
        Assert.All(slices, x => Assert.InRange(strict.GetByteCount(x.Diff), 1, 512));
        var batches = RepositoryReviewPlan.Batches([text], 512);
        Assert.Equal(text, string.Concat(batches)); Assert.All(batches, x => Assert.InRange(strict.GetByteCount(x), 1, 512));
        Assert.Equal("review_segment_limit", RepositoryReviewService.Split(diff, 8).Error?.Code);
    }

    [Fact]
    public void ManySmallFilesShareAnalysisCallsWithoutLosingSource()
    {
        var diff = Diff(20, 100);
        var snapshot = RepositoryReviewPlan.Create(diff, new() { ContextTokens = 12000, MaxOutputTokens = 10000 }, "team/repo", new string('a', 40), null, "", "review").Value!;
        Assert.True(snapshot.Slices.Length < 20); Assert.Equal(diff, RepositoryReviewPlan.Source(snapshot.Slices));
        Assert.All(snapshot.Slices, x => Assert.InRange(Encoding.UTF8.GetByteCount(x.Diff), 1, snapshot.InputBudget));
    }

    [Fact]
    public void MaximumFocusNoteStillLeavesRoomForFramingAndCorrection()
    {
        var note = new string('x', 2000); var head = new string('a', 40);
        var snapshot = RepositoryReviewPlan.Create(Diff(3, 9000), new() { ContextTokens = 32768, MaxOutputTokens = 4096 }, "team/repo", head, null, note, "review").Value!;
        var context = RepositoryReviewPlan.Context("team/repo", head, null, note);
        Assert.All(snapshot.Slices, slice => Assert.True(context.Length + slice.Label.Length + slice.Diff.Length + 1024 < ModelTaskService.MaxPromptCharacters));
        Assert.Equal(Diff(3, 9000), RepositoryReviewPlan.Source(snapshot.Slices));
    }

    [Fact]
    public void DiffSplittingPreservesAllTextAndBoundsLargeOrBinaryChanges()
    {
        var diff = "diff --git a/long.cs b/long.cs\n@@ -1 +1 @@\n+" + new string('x', 8000) + "\n";
        var slices = RepositoryReviewService.Split(diff, 512).Value!; Assert.Equal(diff, string.Concat(slices.Select(x => x.Diff))); Assert.All(slices, x => Assert.InRange(x.Diff.Length, 1, 512));
        Assert.True(Assert.Single(RepositoryReviewService.Split("diff --git a/a.png b/a.png\nBinary files a/a.png and b/a.png differ\n", 1000).Value!).Binary);
        Assert.Equal("review_no_changes", RepositoryReviewService.Split("", 1000).Error?.Code);
        Assert.Equal("repository_diff_limit", RepositoryReviewService.Split("diff --git a/a b/a\n" + new string('x', 257000), 12000).Error?.Code);
    }
}
