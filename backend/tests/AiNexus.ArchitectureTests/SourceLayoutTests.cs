using Xunit;

namespace AiNexus.ArchitectureTests;

/// <summary>
/// A type is found by its file name and a file by its folder: each file is named after the main type it declares, and
/// its namespace is the project's root namespace followed by its folders. Helper types (a use case's DTOs and validator,
/// an entity's configuration) may share the file; a type other files also use gets its own file.
/// </summary>
public sealed class SourceLayoutTests
{
    public static TheoryData<string> Projects => ["AiNexus.Host", "AiNexus.Features", "AiNexus.Platform"];

    [Theory]
    [MemberData(nameof(Projects))]
    public void Every_file_declares_the_type_it_is_named_after(string project)
    {
        var mismatched = SourceTree.Files(project)
            .Where(path => !SourceTree.TopLevelTypes(path).Contains(Path.GetFileNameWithoutExtension(path)))
            .Select(path => $"{SourceTree.Relative(path)}: [{string.Join(", ", SourceTree.TopLevelTypes(path))}]").Order(StringComparer.Ordinal).ToList();
        Assert.True(mismatched.Count == 0, "Name the file after its main type, or move the other types to files of their own:\n" + string.Join("\n", mismatched));
    }

    [Theory]
    [MemberData(nameof(Projects))]
    public void Namespaces_follow_folders(string project)
    {
        var mismatched = SourceTree.Files(project).Select(path => (Path: SourceTree.Relative(path), Actual: SourceTree.Namespace(path)))
            .Select(x => (x.Path, x.Actual, Expected: string.Join('.', x.Path.Split('/')[..^1])))
            .Where(x => x.Actual != x.Expected).Select(x => $"{x.Path}: {x.Actual} (expected {x.Expected})").Order(StringComparer.Ordinal).ToList();
        Assert.True(mismatched.Count == 0, "Namespaces must match folders:\n" + string.Join("\n", mismatched));
    }
}
