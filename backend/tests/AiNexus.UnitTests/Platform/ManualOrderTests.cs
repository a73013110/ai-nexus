using AiNexus.Platform.Data;

namespace AiNexus.UnitTests.Platform;

/// <summary>與前端 <c>shared/browser/sort-order.spec.ts</c> 同一組情境：兩邊的規則必須一致。</summary>
public sealed class ManualOrderTests
{
    private const int Max = 1_000_000;
    private static readonly (string, int?)[] Mixed = [("a", 10), ("b", 20), ("c", 30), ("d", null), ("e", null)];

    [Fact]
    public void WritesOnlyTheMovedItemBetweenItsNewNeighbours()
    {
        Assert.Equal([("c", 15)], ManualOrder.Plan(Mixed, "c", 1, Max));
        Assert.Equal([("a", 40)], ManualOrder.Plan(Mixed, "a", 2, Max));
        Assert.Equal([("c", 0)], ManualOrder.Plan(Mixed, "c", 0, Max));
    }

    [Fact]
    public void KeepsUnsortedItemsUnsortedUnlessTheMovePassesThem()
    {
        Assert.Equal([("e", 40)], ManualOrder.Plan(Mixed, "e", 3, Max));
        Assert.Equal([("d", 40), ("a", 50)], ManualOrder.Plan(Mixed, "a", 3, Max));
        (string, int?)[] unsorted = [("x", null), ("y", null), ("z", null)];
        Assert.Equal([("z", 10)], ManualOrder.Plan(unsorted, "z", 0, Max));
        Assert.Equal([("y", 10), ("x", 20)], ManualOrder.Plan(unsorted, "x", 1, Max));
    }

    [Fact]
    public void WritesNothingWhenDroppedInPlace()
    {
        Assert.Empty(ManualOrder.Plan(Mixed, "b", 1, Max));
        Assert.Empty(ManualOrder.Plan(Mixed, "e", 4, Max));
        // 超過人數視為最後一位。
        Assert.Empty(ManualOrder.Plan(Mixed, "e", 99, Max));
    }

    [Fact]
    public void RenumbersTheSortedRangeWhenThereIsNoRoomLeft()
    {
        (string, int?)[] tight = [("a", 10), ("b", 11), ("c", 12), ("d", null)];
        Assert.Equal([("c", 20), ("b", 30)], ManualOrder.Plan(tight, "c", 1, Max));
        (string, int?)[] full = [("a", 1), ("b", 2), ("c", 3)];
        Assert.Equal([("b", 1), ("c", 2), ("a", 3)], ManualOrder.Plan(full, "a", 2, 3));
    }

    [Fact]
    public void AlwaysWritesAnItemThatComesFromElsewhere()
    {
        Assert.Equal([("new", 20)], ManualOrder.Plan([("a", 10)], "new", 1, Max));
        Assert.Equal([("new", 10)], ManualOrder.Plan([], "new", 0, Max));
    }
}
