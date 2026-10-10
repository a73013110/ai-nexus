
namespace AiNexus.ArchitectureTests;

/// <summary>
/// A type is found by its file name and a file by its folder: each file is named after the main type it declares, and
/// its namespace is the project's root namespace followed by its folders. Helper types (a use case's DTOs and validator,
/// an entity's configuration) may share the file; a type other files also use gets its own file. Tests follow the same
/// rules, and are filed by the module whose use case they cover, so a change to one module runs one folder.
/// </summary>
public sealed class SourceLayoutTests
{
    public static TheoryData<string> Projects =>
        ["AiNexus.Host", "AiNexus.Features", "AiNexus.Platform", "AiNexus.UnitTests", "AiNexus.IntegrationTests", "AiNexus.ArchitectureTests"];

    public static TheoryData<string> TestProjects => ["AiNexus.UnitTests", "AiNexus.IntegrationTests", "AiNexus.ArchitectureTests"];

    public static TheoryData<string> ModuleTestProjects => ["AiNexus.UnitTests", "AiNexus.IntegrationTests"];

    [Theory]
    [MemberData(nameof(Projects))]
    public void EveryFileDeclaresTheTypeItIsNamedAfter(string project)
    {
        var mismatched = SourceTree.Files(project)
            .Where(path => !SourceTree.TopLevelTypes(path).Contains(Path.GetFileNameWithoutExtension(path)))
            .Select(path => $"{SourceTree.Relative(path)}: [{string.Join(", ", SourceTree.TopLevelTypes(path))}]").Order(StringComparer.Ordinal).ToList();
        Assert.True(mismatched.Count == 0, "Name the file after its main type, or move the other types to files of their own:\n" + string.Join("\n", mismatched));
    }

    [Theory]
    [MemberData(nameof(Projects))]
    public void NamespacesFollowFolders(string project)
    {
        var mismatched = SourceTree.Files(project).Select(path => (Path: SourceTree.Relative(path), Actual: SourceTree.Namespace(path)))
            .Select(x => (x.Path, x.Actual, Expected: string.Join('.', x.Path.Split('/')[..^1])))
            .Where(x => x.Actual != x.Expected).Select(x => $"{x.Path}: {x.Actual} (expected {x.Expected})").Order(StringComparer.Ordinal).ToList();
        Assert.True(mismatched.Count == 0, "Namespaces must match folders:\n" + string.Join("\n", mismatched));
    }

    [Theory]
    [MemberData(nameof(ModuleTestProjects))]
    public void TestsAreFiledByModule(string project)
    {
        var folders = SourceTree.Modules().Union(["Host", "Platform", "Support"]).ToHashSet(StringComparer.Ordinal);
        var misplaced = SourceTree.Files(project).Select(SourceTree.Relative).Select(path => path.Split('/'))
            .Where(parts => parts.Length < 3 || !folders.Contains(parts[1]) || (parts[1] != "Support" && !parts[^1].EndsWith("Tests.cs", StringComparison.Ordinal)))
            .Select(parts => string.Join('/', parts)).Order(StringComparer.Ordinal).ToList();
        Assert.True(misplaced.Count == 0, "Put tests in <Module>/<UseCase>Tests.cs (or Host, Platform) and shared helpers in Support:\n" + string.Join("\n", misplaced));
    }

    /// <summary>A test name states the behavior as a PascalCase sentence; the class already names the use case.</summary>
    [Theory]
    [MemberData(nameof(TestProjects))]
    public void TestMethodsArePascalCaseSentences(string project)
    {
        var offending = SourceTree.Files(project).SelectMany(path => SourceTree.TestMethods(path).Select(name => (Path: SourceTree.Relative(path), Name: name)))
            .Where(x => !char.IsUpper(x.Name[0]) || x.Name.Contains('_', StringComparison.Ordinal) || x.Name.EndsWith("Async", StringComparison.Ordinal))
            .Select(x => $"{x.Path}: {x.Name}").Order(StringComparer.Ordinal).ToList();
        Assert.True(offending.Count == 0, "Name tests as PascalCase sentences without underscores or an Async suffix:\n" + string.Join("\n", offending));
        Assert.NotEmpty(SourceTree.Files(project).SelectMany(SourceTree.TestMethods));
    }
}
