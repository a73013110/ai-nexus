using System.Text.RegularExpressions;

namespace AiNexus.ArchitectureTests;

/// <summary>The C# sources under backend/src and backend/tests, read from the repository the tests were built from.</summary>
internal static partial class SourceTree
{
    public static readonly string Root = FindRoot();

    /// <summary>Hand-written source files: generated migrations and the host's top-level program are left out.</summary>
    public static IEnumerable<string> Files(string project) => Directory
        .EnumerateFiles(Path.Combine(Root, "backend", Area(project), project), "*.cs", SearchOption.AllDirectories)
        .Where(path => !Relative(path).Split('/').Any(part => part is "bin" or "obj" or "Migrations"))
        .Where(path => !(project == "AiNexus.Host" && Path.GetFileName(path) == "Program.cs"));

    /// <summary>Path relative to backend/src or backend/tests with forward slashes, starting with the project folder.</summary>
    public static string Relative(string path)
    {
        var relative = Path.GetRelativePath(Path.Combine(Root, "backend"), path).Replace('\\', '/');
        return relative[(relative.IndexOf('/', StringComparison.Ordinal) + 1)..];
    }

    /// <summary>Feature module folders of AiNexus.Features.</summary>
    public static IReadOnlySet<string> Modules() => Directory.EnumerateDirectories(Path.Combine(Root, "backend", "src", "AiNexus.Features"))
        .Select(Path.GetFileName).OfType<string>().Where(name => name is not ("bin" or "obj")).ToHashSet(StringComparer.Ordinal);

    private static string Area(string project) => project.EndsWith("Tests", StringComparison.Ordinal) ? "tests" : "src";

    /// <summary>Type names declared at the top level of a file-scoped namespace (declarations start at column 0).</summary>
    public static IReadOnlyList<string> TopLevelTypes(string path) => [.. File.ReadLines(path)
        .Select(line => Declaration().Match(line)).Where(m => m.Success).Select(m => m.Groups["name"].Value)];

    public static string? Namespace(string path) => File.ReadLines(path)
        .Select(line => FileNamespace().Match(line)).FirstOrDefault(m => m.Success)?.Groups["name"].Value;

    /// <summary>Names of the [Fact] and [Theory] methods in a file.</summary>
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
