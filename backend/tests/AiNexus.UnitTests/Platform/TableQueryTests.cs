using AiNexus.Platform.Data.Tables;

namespace AiNexus.UnitTests.Platform;

public sealed class TableQueryTests
{
    private enum Stage { Draft, Published }

    private sealed record Row(int Id, string Name, Stage Stage, bool Archived, int? Size, DateTimeOffset At);

    private static readonly Row[] Rows =
    [
        new(1, "Alpha report", Stage.Draft, false, 10, new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
        new(2, "beta: notes", Stage.Published, true, null, new(2026, 2, 1, 0, 0, 0, TimeSpan.Zero)),
        new(3, "Gamma", Stage.Published, false, 30, new(2026, 3, 1, 0, 0, 0, TimeSpan.Zero)),
        new(4, "Alpha draft", Stage.Draft, false, 30, new(2026, 3, 1, 0, 0, 0, TimeSpan.Zero)),
    ];

    private static TableColumns<Row> Columns() => new TableColumns<Row>()
        .Text("name", x => x.Name)
        .Options("stage", x => x.Stage)
        .Options("archived", x => x.Archived)
        .Range("size", x => x.Size)
        .Range("at", x => x.At, TableUse.Filter)
        .Key(x => x.Id)
        .DefaultSort("name");

    private static int[] Ids(TableRequest request)
    {
        var parsed = Columns().Parse(request);
        Assert.True(parsed.IsSuccess);
        return parsed.Value.Order(parsed.Value.Filter(Rows.AsQueryable())).Select(x => x.Id).ToArray();
    }

    [Fact]
    public void DefaultSortBreaksTiesWithKey() =>
        Assert.Equal([4, 1, 2, 3], Ids(new TableRequest()));

    [Fact]
    public void SortsDescendingWithKeyStillAscending() =>
        Assert.Equal([3, 4, 1, 2], Ids(new TableRequest { Sort = "size:desc" }));

    [Fact]
    public void TextFilterKeepsColonsInValue() =>
        Assert.Equal([2], Ids(new TableRequest { Filter = ["name:has:beta: notes"] }));

    [Fact]
    public void OptionsParseEnumNamesAndBooleans()
    {
        Assert.Equal([2, 3], Ids(new TableRequest { Filter = ["stage:in:published"] }));
        Assert.Equal([4, 1, 3], Ids(new TableRequest { Filter = ["archived:in:false", "stage:in:Draft,Published"] }));
    }

    [Fact]
    public void RangeFiltersCombineAndSkipNulls()
    {
        Assert.Equal([4, 3], Ids(new TableRequest { Filter = ["size:ge:20", "size:le:30"] }));
        Assert.Equal([2], Ids(new TableRequest { Filter = ["at:ge:2026-02-01T00:00:00Z", "at:lt:2026-03-01T00:00:00Z"] }));
    }

    [Theory]
    [InlineData("missing", null)]
    [InlineData("at", null)]
    [InlineData("name:up", null)]
    [InlineData(null, "missing:has:x")]
    [InlineData(null, "name:in:x")]
    [InlineData(null, "stage:in:archived")]
    [InlineData(null, "size:ge:abc")]
    [InlineData(null, "name:has:")]
    [InlineData(null, "name")]
    public void RejectsUndeclaredColumnsOperationsAndValues(string? sort, string? filter) =>
        Assert.False(Columns().Parse(new TableRequest { Sort = sort, Filter = filter is null ? null : [filter] }).IsSuccess);

    [Theory]
    [InlineData(-1, 50)]
    [InlineData(0, 0)]
    [InlineData(0, 201)]
    public void RejectsPagingOutOfRange(int offset, int limit) =>
        Assert.Equal(TableQueryExtensions.Invalid, Columns().Parse(new TableRequest { Offset = offset, Limit = limit }).Error);

    [Fact]
    public void ColumnWithoutKeyIsProgrammingError() =>
        Assert.Throws<InvalidOperationException>(() => new TableColumns<Row>().Text("name", x => x.Name).Parse(new TableRequest()));
}
