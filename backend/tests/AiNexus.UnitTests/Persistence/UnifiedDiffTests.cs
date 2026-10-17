using AiNexus.Features.Persistence.ReleaseScripts;

namespace AiNexus.UnitTests.Persistence;

public sealed class UnifiedDiffTests
{
    [Fact]
    public void EqualLinesHaveNoDiff()
    {
        Assert.Equal("", UnifiedDiff.Create("a", "b", ["x", "y"], ["x", "y"]));
    }

    [Fact]
    public void ChangedLineIsShownWithThreeLinesOfContext()
    {
        string[] before = ["1", "2", "3", "4", "5", "6", "7", "8", "9"];
        string[] after = ["1", "2", "3", "4", "five", "6", "7", "8", "9"];
        Assert.Equal(Text("--- a", "+++ b", "@@ -2,7 +2,7 @@", " 2", " 3", " 4", "-5", "+five", " 6", " 7", " 8"),
            UnifiedDiff.Create("a", "b", before, after));
    }

    [Fact]
    public void DistantChangesGetSeparateHunksAndNearOnesShareOne()
    {
        var before = Enumerable.Range(1, 30).Select(x => x.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        var after = before.ToArray();
        after[1] = "two";
        after[8] = "nine";
        after[25] = "twenty-six";
        var diff = UnifiedDiff.Create("a", "b", before, after);
        // 第 2 行與第 9 行之間只隔 6 行未變動，兩者各自的 3 行上下文剛好相接，因此合併成同一個 hunk；
        // 第 26 行距離夠遠，上下文不相交，另成一個 hunk。
        Assert.Equal(["@@ -1,12 +1,12 @@", "@@ -23,7 +23,7 @@"], diff.Split(Environment.NewLine).Where(x => x.StartsWith("@@", StringComparison.Ordinal)));
    }

    [Fact]
    public void AddedFileStartsFromAnEmptyRange()
    {
        Assert.Equal(Text("--- /dev/null", "+++ b", "@@ -0,0 +1,2 @@", "+x", "+y"), UnifiedDiff.Create(UnifiedDiff.Missing, "b", [], ["x", "y"]));
    }

    [Fact]
    public void RemovedLinesKeepTheLongestCommonSubsequence()
    {
        Assert.Equal(Text("--- a", "+++ b", "@@ -1,5 +1,4 @@", " a", "-b", " c", "+x", " d", "-e"),
            UnifiedDiff.Create("a", "b", ["a", "b", "c", "d", "e"], ["a", "c", "x", "d"]));
    }

    [Fact]
    public void LinesIgnoreLineEndingsAndTheFinalBreak()
    {
        Assert.Equal(["a", "b", ""], UnifiedDiff.Lines("a\r\nb\n\n"));
        Assert.Equal(["a"], UnifiedDiff.Lines("a"));
        Assert.Empty(UnifiedDiff.Lines(""));
    }

    private static string Text(params string[] lines) => string.Concat(lines.Select(x => x + Environment.NewLine));
}
