
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

    /// <summary>
    /// A module stays flat up to <see cref="MaxModuleRootFiles"/> files. A larger one files its use cases, entities and
    /// services in capability folders one level below the module, and its root keeps only what spans the module.
    /// </summary>
    [Fact]
    public void LargeModulesAreSplitIntoCapabilityFolders()
    {
        var paths = SourceTree.Files("AiNexus.Features").Select(SourceTree.Relative).Select(path => path.Split('/'))
            .Where(parts => parts.Length > 2 && parts[1] != "Persistence").ToList();
        var crowded = paths.Where(parts => parts.Length == 3).GroupBy(parts => parts[1])
            .Where(module => module.Count() > MaxModuleRootFiles).Select(module => $"{module.Key}: {module.Count()} files at the root");
        var nested = paths.Where(parts => parts.Length > 4).Select(parts => string.Join('/', parts));
        var offending = crowded.Concat(nested).Order(StringComparer.Ordinal).ToList();
        Assert.True(offending.Count == 0, "Group a large module's files into capability folders, one level deep:\n" + string.Join("\n", offending));
    }

    private const int MaxModuleRootFiles = 15;

    /// <summary>
    /// A frontend feature folder is the kebab-case name of the backend module that owns its API, so either side finds
    /// the other by name (ADR 0009). <c>design</c> is the design-system gallery and has no backend.
    /// </summary>
    [Fact]
    public void FrontendFeaturesAreNamedAfterBackendModules()
    {
        var modules = SourceTree.Modules();
        var unmatched = Directory.EnumerateDirectories(Path.Combine(SourceTree.Root, "frontend", "src", "app", "features"))
            .Select(Path.GetFileName).OfType<string>().Where(folder => folder != "design")
            .Where(folder => !modules.Contains(string.Concat(folder.Split('-').Select(part => char.ToUpperInvariant(part[0]) + part[1..]))))
            .Order(StringComparer.Ordinal).ToList();
        Assert.True(unmatched.Count == 0, "Name frontend feature folders after the backend module in kebab-case:\n" + string.Join("\n", unmatched));
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
