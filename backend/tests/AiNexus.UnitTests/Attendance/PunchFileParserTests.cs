using System.Text;
using AiNexus.Features.Office.Attendance.Imports;

namespace AiNexus.UnitTests.Attendance;

public sealed class PunchFileParserTests
{
    private static readonly TimeSpan Taipei = TimeSpan.FromHours(8);

    [Fact]
    public void ALineBecomesAPunchInTaipeiTimeKeepingTheFirstAndLastFieldsAsTheyAre()
    {
        var punch = Assert.Single(PunchFileParser.Parse("007:0012345:20261001:082915:15"));

        Assert.Equal(("0012345", "007", "15"), (punch.CardNumber, punch.MachineCode, punch.EventCode));
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 8, 29, 15, Taipei), punch.PunchedAt);
        Assert.Equal(Taipei, punch.PunchedAt.Offset);
        Assert.Equal(new DateOnly(2026, 10, 1), punch.AttendanceDate);
    }

    [Fact]
    public void TheAttendanceDateFollowsThePunchTimeNotTheFileContentsAround()
    {
        var punches = PunchFileParser.Parse("001:A1:20261001:235800:11\n001:A1:20261002:000300:11\n");

        Assert.Equal([new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 2)], punches.Select(x => x.AttendanceDate));
        // 23:58 台灣時間是同一天的 15:58 UTC，出勤日不能被 UTC 帶到別天。
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 15, 58, 0, TimeSpan.Zero), punches[0].PunchedAt);
    }

    [Fact]
    public void BlankLinesCarriageReturnsAndTheDosEndOfFileMarkAreIgnored()
    {
        var punches = PunchFileParser.Parse("\r\n001:A1:20261001:080000:11\r\n   \r\n001:A2:20261001:080100:11\r\n\u001A");

        Assert.Equal(["A1", "A2"], punches.Select(x => x.CardNumber));
    }

    [Fact]
    public void ARepeatedPunchInTheSameFileKeepsTheFirstOne()
    {
        var punches = PunchFileParser.Parse("001:a1:20261001:080000:11\n001:A1:20261001:080000:99\n002:A1:20261001:080000:11\n");

        Assert.Equal([("a1", "001", "11"), ("A1", "002", "11")], punches.Select(x => (x.CardNumber, x.MachineCode, x.EventCode)));
    }

    [Theory]
    [InlineData("001:A1:20261001:080000", "欄位數")]
    [InlineData("001:A1:20261001:080000:11:extra", "欄位數")]
    [InlineData("001::20261001:080000:11", "卡號")]
    [InlineData(":A1:20261001:080000:11", "機器代號")]
    [InlineData("001:A1:20261001:080000:", "事件代號")]
    [InlineData("001:A1:20261301:080000:11", "日期")]
    [InlineData("001:A1:2026-10-01:080000:11", "日期")]
    [InlineData("001:A1:19991231:080000:11", "日期")]
    [InlineData("001:A1:20261001:246000:11", "時間")]
    [InlineData("001:A1:20261001:0800:11", "時間")]
    [InlineData("001:A1:20261001:08:00:00:11", "欄位數")]
    [InlineData("001:A1\u0007:20261001:080000:11", "卡號")]
    public void AMalformedLineFailsTheWholeFileAndNamesTheLine(string bad, string reason)
    {
        var error = Assert.Throws<PunchFileFormatException>(() => PunchFileParser.Parse("001:ZZ0:20261001:075900:11\n" + bad + "\n001:A2:20261001:080100:11\n"));

        Assert.Contains("第 2 行", error.Message);
        Assert.Contains(reason, error.Message);
        // 不回顯檔案內容。
        Assert.DoesNotContain("ZZ0", error.Message);
    }

    [Fact]
    public void ManyBadLinesAreCountedAndOnlyTheFirstFewAreListed()
    {
        var text = string.Concat(Enumerable.Range(0, 10).Select(_ => "garbage\n"));

        var error = Assert.Throws<PunchFileFormatException>(() => PunchFileParser.Parse(text));

        Assert.Contains("10 行", error.Message);
        Assert.Contains("第 3 行", error.Message);
        Assert.DoesNotContain("第 4 行", error.Message);
    }

    [Fact]
    public void Utf8WithOrWithoutABomIsDecodedAndInvalidBytesAreRejected()
    {
        var text = "001:A1:20261001:080000:11\n";
        var plain = Encoding.UTF8.GetBytes(text);

        Assert.Equal(text, PunchFileParser.Decode(plain));
        Assert.Equal(text, PunchFileParser.Decode([.. Encoding.UTF8.GetPreamble(), .. plain]));
        Assert.Equal(text, PunchFileParser.Decode([.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes(text)]));
        Assert.Throws<PunchFileFormatException>(() => PunchFileParser.Decode([0x30, 0xA4, 0xE5, 0x30]));
    }

    [Theory]
    [InlineData("20261001.txt", true, 2026, 10, 1)]
    [InlineData("20261001.TXT", true, 2026, 10, 1)]
    [InlineData("20261301.txt", false, 0, 0, 0)]
    [InlineData("20261001.txt.bak", false, 0, 0, 0)]
    [InlineData("2026100.txt", false, 0, 0, 0)]
    [InlineData("notes.txt", false, 0, 0, 0)]
    [InlineData("20261001.csv", false, 0, 0, 0)]
    public void OnlyDailyFileNamesAreRecognised(string name, bool valid, int year, int month, int day)
    {
        Assert.Equal(valid, PunchFileParser.TryParseFileName(name, out var date));
        if (valid) Assert.Equal(new DateOnly(year, month, day), date);
    }

    [Fact]
    public void TheFileNameForADateIsTheDailyFileName() => Assert.Equal("20260105.txt", PunchFileParser.FileNameFor(new DateOnly(2026, 1, 5)));
}
