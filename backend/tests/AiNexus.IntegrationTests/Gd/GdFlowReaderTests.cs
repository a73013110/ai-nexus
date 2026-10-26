using AiNexus.Features.Platform.Gd;

namespace AiNexus.IntegrationTests.Gd;

public sealed class GdFlowReaderTests
{
    private static Task<IReadOnlyList<GdFlowStep>?> ReadAsync(FakeGd gd, string desNo, bool active = true)
        => new GdFlowReader(gd.Access()).ReadAsync(desNo, active, TestContext.Current.CancellationToken);

    private static FakeGd Create()
    {
        var gd = FakeGd.Create();
        gd.AddDepartment(7, "資訊組");
        gd.AddUser("G001", "alice", name: "林予安");
        gd.AddUser("G002", "bob", name: "陳品睿");
        gd.AddUser("G003", "carol", name: "張文彥");
        gd.AddUser("G009", "dave", name: "吳思妤");
        gd.AddMembership(7, "G001", "T1", "專員");
        gd.AddMembership(7, "G003", "T2", "組長");
        return gd;
    }

    [Fact]
    public async Task HistoryCurrentAndNextStepsAreReadInOrderWithNamesUnitsAndTitles()
    {
        using var gd = Create();
        gd.AddFlow("FLOW_HISTORY", "L115102201", "002", "7_G002", "請假代理人", "2026/10/22 09:02:00", "如承辦人所擬");
        gd.AddFlow("FLOW_HISTORY", "L115102201", "001", "7_G001", "待處理表單", "2026/10/22 08:45:00");
        gd.AddFlow("FLOW_CURRENT", "L115102201", "003", "7_G003", "串簽", "2026/10/22 09:02:30");
        gd.AddFlow("FLOW_NEXT", "L115102201", "004", "9_G009", "決行");
        gd.AddFlow("FLOW_HISTORY", "L115102299", "001", "7_G001", "待處理表單", "2026/10/22 08:00:00");

        var steps = await ReadAsync(gd, "L115102201");

        Assert.NotNull(steps);
        Assert.Equal([GdFlowStepState.Done, GdFlowStepState.Done, GdFlowStepState.Current, GdFlowStepState.Pending], steps.Select(x => x.State));
        // 「待處理表單」是送出；其他 STATUS 照原字。
        Assert.Equal(["送出", "請假代理人", "串簽", "決行"], steps.Select(x => x.Action));
        Assert.Equal(new GdPerson("林予安", "資訊組", "專員"), steps[0].Person);
        Assert.Equal(new GdPerson("陳品睿", "資訊組", null), steps[1].Person);
        Assert.Equal("如承辦人所擬", steps[1].Memo);
        Assert.Equal(new DateTimeOffset(2026, 10, 22, 8, 45, 0, TimeSpan.FromHours(8)), steps[0].At);
        // 部門 9 不存在，仍有姓名。
        Assert.Equal(new GdPerson("吳思妤", null, null), steps[3].Person);
    }

    [Fact]
    public async Task AFinishedFlowIsReadFromTheBackupTableOfTheYearInTheFormNumber()
    {
        using var gd = Create();
        gd.AddBackupTable("115");
        gd.AddBackupTable("114");
        gd.AddFlow("FLOW_BACKUP_115", "T115100501", "001", "7_G001", "待處理表單", "2026/10/05 09:10:00");
        gd.AddFlow("FLOW_BACKUP_115", "T115100501", "002", "7_G003", "串簽", "2026/10/06 14:02:00", "通過審核");
        gd.AddFlow("FLOW_BACKUP_114", "T115100501", "009", "7_G009", "串簽", "2025/10/06 14:02:00");
        // 結案後留在 FLOW_CURRENT、FLOW_NEXT 的列不當成還在等。
        gd.AddFlow("FLOW_CURRENT", "T115100501", "003", "7_G009", "流程終結");
        gd.AddFlow("FLOW_NEXT", "T115100501", "004", "7_G009", "決行");

        var steps = await ReadAsync(gd, "T115100501", active: false);

        Assert.NotNull(steps);
        Assert.Equal(["送出", "串簽"], steps.Select(x => x.Action));
        Assert.All(steps, x => Assert.Equal(GdFlowStepState.Done, x.State));
    }

    [Fact]
    public async Task AMissingBackupTableOnlySkipsTheBackup()
    {
        using var gd = Create();
        gd.AddFlow("FLOW_HISTORY", "L099010101", "001", "7_G001", "待處理表單", "2010/01/01 08:00:00");
        gd.AddFlow("FLOW_HISTORY", "LX99", "001", "7_G001", "待處理表單", "not a time");

        Assert.Equal(["送出"], (await ReadAsync(gd, "L099010101"))!.Select(x => x.Action));
        // 單號第 2～4 碼不是數字時不查備份表；時間解析不了就當沒有。
        Assert.Null(Assert.Single((await ReadAsync(gd, "LX99"))!).At);
    }

    [Fact]
    public async Task AnAgentRecordedInOriginIdIsNamed()
    {
        using var gd = Create();
        gd.AddFlow("FLOW_CURRENT", "L115102201", "003", "7_G003", "串簽", "2026/10/22 09:02:30", origin: "7_G002");

        var step = Assert.Single((await ReadAsync(gd, "L115102201"))!);

        Assert.Equal("陳品睿", step.Agent);
    }

    [Fact]
    public async Task AnUnreachableGdIsNotAnEmptyFlow()
    {
        Assert.Null(await new GdFlowReader(FakeGd.AccessTo(FakeGd.Unreachable())).ReadAsync("L115102201", true, TestContext.Current.CancellationToken));
        using var gd = Create();
        Assert.Empty((await ReadAsync(gd, "L115102201"))!);
    }
}
