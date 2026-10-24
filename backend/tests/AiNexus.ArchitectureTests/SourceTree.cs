using System.Text.RegularExpressions;

namespace AiNexus.ArchitectureTests;

/// <summary>backend/src 與 backend/tests 底下的 C# 原始碼，以建置測試時所在的儲存庫為根。</summary>
internal static partial class SourceTree
{
    public static readonly string Root = FindRoot();

    /// <summary>原始碼檔；排除 Host 的頂層 Program.cs。</summary>
    public static IEnumerable<string> Files(string project) => Directory
        .EnumerateFiles(Path.Combine(Root, "backend", Area(project), project), "*.cs", SearchOption.AllDirectories)
        .Where(path => !Relative(path).Split('/').Any(part => part is "bin" or "obj"))
        .Where(path => !(project == "AiNexus.Host" && Path.GetFileName(path) == "Program.cs"));

    /// <summary>相對於 backend/src 或 backend/tests 的路徑，使用正斜線，開頭為專案資料夾名稱。</summary>
    public static string Relative(string path)
    {
        var relative = Path.GetRelativePath(Path.Combine(Root, "backend"), path).Replace('\\', '/');
        return relative[(relative.IndexOf('/', StringComparison.Ordinal) + 1)..];
    }

    /// <summary>AiNexus.Features 的根目錄。</summary>
    public static string FeaturesRoot => Path.Combine(Root, "backend", "src", "AiNexus.Features");

    /// <summary>
    /// 允許的區（小寫，ADR 0016）。區只是資料夾、不是依賴邊界；新增區要先修訂 ADR。
    /// <c>office</c>（公司事務）由 Attendance 使用，之後的公文、客服也放這裡。
    /// </summary>
    public static readonly IReadOnlySet<string> AllowedAreas = new HashSet<string>(["work", "office", "admin", "platform"], StringComparer.Ordinal);

    /// <summary>AiNexus.Features 底下的區資料夾名稱（排除建置輸出）。</summary>
    public static IReadOnlyList<string> Areas() => [.. Directory.EnumerateDirectories(FeaturesRoot)
        .Select(Path.GetFileName).OfType<string>().Where(name => name is not ("bin" or "obj")).Order(StringComparer.Ordinal)];

    /// <summary>AiNexus.Features/&lt;Area&gt;/&lt;Module&gt; 的區與模組資料夾名稱。</summary>
    public static IReadOnlyList<(string Area, string Module)> ModuleFolders() => [.. Areas()
        .SelectMany(area => Directory.EnumerateDirectories(Path.Combine(FeaturesRoot, area)).Select(Path.GetFileName).OfType<string>()
            .Order(StringComparer.Ordinal).Select(module => (area, module)))];

    /// <summary>所有區底下的模組名稱；模組名全域唯一，測試專案依模組平鋪、不分區。</summary>
    public static IReadOnlySet<string> Modules() => ModuleFolders().Select(x => x.Module).ToHashSet(StringComparer.Ordinal);

    private static string Area(string project) => project.EndsWith("Tests", StringComparison.Ordinal) ? "tests" : "src";

    /// <summary>檔案範圍 namespace 下頂層宣告的型別名。只認第 0 欄開始的宣告，巢狀型別因為有縮排而不會被列入。</summary>
    public static IReadOnlyList<string> TopLevelTypes(string path) => [.. File.ReadLines(path)
        .Select(line => Declaration().Match(line)).Where(m => m.Success).Select(m => m.Groups["name"].Value)];

    public static string? Namespace(string path) => File.ReadLines(path)
        .Select(line => FileNamespace().Match(line)).FirstOrDefault(m => m.Success)?.Groups["name"].Value;

    /// <summary>檔案中 [Fact] 與 [Theory] 測試方法的名稱。</summary>
    public static IEnumerable<string> TestMethods(string path) => TestMethod().Matches(File.ReadAllText(path)).Select(m => m.Groups["name"].Value);

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "global.json"))) return directory.FullName;
        throw new InvalidOperationException("Repository root (global.json) was not found.");
    }

    [GeneratedRegex(@"^(?:\[.*\]\s*)?(?:(?:public|internal|file|sealed|static|abstract|partial|readonly|ref)\s+)*(?:class|interface|enum|struct|record(?:\s+(?:class|struct))?)\s+(?<name>[A-Za-z_]\w*)")]
    private static partial Regex Declaration();

    [GeneratedRegex(@"^namespace\s+(?<name>[\w.]+)\s*;")]
    private static partial Regex FileNamespace();

    [GeneratedRegex(@"\[(?:Fact|Theory)\b[^\n]*\n(?:\s*\[[^\n]*\n)*\s*public\s+(?:async\s+)?\w+\s+(?<name>\w+)\(")]
    private static partial Regex TestMethod();
}
