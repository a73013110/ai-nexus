using System.Globalization;
using System.Text.RegularExpressions;

namespace AiNexus.Features.Admin.Releases;

/// <summary>
/// 版號 <c>YYYY.MM.N</c>（ADR 0027）與它的建置後綴。同一個 YYYY.MM.N 內：<c>-preview</c>（下一版的試裝包）早於正式版，
/// <c>-dev.K</c>（tag 之後 K 個 commit 的建置，見 <c>Get-RepoVersion</c>）晚於正式版，所以不能照 SemVer 比。
/// </summary>
public sealed partial record ReleaseVersion(int Year, int Month, int Number, int Stage, int Commits) : IComparable<ReleaseVersion>
{
    private const int Preview = -1, Release = 0, Development = 1;

    /// <summary>無法解析（例如舊庫留下的文字）時回傳 null；呼叫端當作「不知道」，不可當作最舊或最新。</summary>
    public static ReleaseVersion? Parse(string? text)
    {
        if (text is null || Pattern().Match(text) is not { Success: true } match) return null;
        var number = (string group) => int.Parse(match.Groups[group].Value, CultureInfo.InvariantCulture);
        var (stage, commits) = match.Groups["suffix"].Value switch
        {
            "" => (Release, 0),
            "-preview" => (Preview, 0),
            "-dev" => (Development, 0),
            _ => (Development, number("commits")),
        };
        return new(number("year"), number("month"), number("number"), stage, commits);
    }

    public int CompareTo(ReleaseVersion? other) => other is null ? 1
        : (Year, Month, Number, Stage, Commits).CompareTo((other.Year, other.Month, other.Number, other.Stage, other.Commits));

    public static bool operator <(ReleaseVersion left, ReleaseVersion right) => left.CompareTo(right) < 0;
    public static bool operator >(ReleaseVersion left, ReleaseVersion right) => left.CompareTo(right) > 0;
    public static bool operator <=(ReleaseVersion left, ReleaseVersion right) => left.CompareTo(right) <= 0;
    public static bool operator >=(ReleaseVersion left, ReleaseVersion right) => left.CompareTo(right) >= 0;

    [GeneratedRegex(@"^(?<year>\d{1,4})\.(?<month>\d{1,2})\.(?<number>\d+)(?<suffix>-preview|-dev(\.(?<commits>\d+))?)?$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
