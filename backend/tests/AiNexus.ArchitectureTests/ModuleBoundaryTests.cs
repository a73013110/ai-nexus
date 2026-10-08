using NetArchTest.Rules;
using Xunit;

namespace AiNexus.ArchitectureTests;

/// <summary>
/// Cross-module dependencies may only shrink. The baseline lists every module-to-module edge that existed when module
/// boundaries were introduced; new code must use the target module's contracts or domain events instead of adding edges.
/// When an edge disappears, delete its line so it cannot silently come back.
/// </summary>
public sealed class ModuleBoundaryTests
{
    private const string Root = "AiNexus.Features.";

    // Shared infrastructure of the Features assembly, not business modules.
    private static readonly HashSet<string> Infrastructure = ["Persistence", "Configuration"];

    [Fact]
    public void Cross_module_dependencies_never_grow()
    {
        var actual = Edges();
        var baseline = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "module-dependencies.baseline.txt"))
            .Select(x => x.Trim()).Where(x => x.Length > 0 && !x.StartsWith('#')).ToHashSet(StringComparer.Ordinal);
        var added = actual.Except(baseline).Order(StringComparer.Ordinal).ToList();
        var removed = baseline.Except(actual).Order(StringComparer.Ordinal).ToList();
        Assert.True(added.Count == 0, "New cross-module dependencies (use the module's contracts or a domain event instead):\n" + string.Join('\n', added));
        Assert.True(removed.Count == 0, "These dependencies are gone; delete them from module-dependencies.baseline.txt:\n" + string.Join('\n', removed));
    }

    internal static HashSet<string> Edges()
    {
        var modules = Assemblies.Features.GetTypes()
            .Select(t => t.Namespace).OfType<string>().Where(ns => ns.StartsWith(Root, StringComparison.Ordinal))
            .Select(ns => ns[Root.Length..].Split('.')[0]).Where(m => !Infrastructure.Contains(m)).ToHashSet(StringComparer.Ordinal);
        var edges = new HashSet<string>(StringComparer.Ordinal);
        foreach (var from in modules)
        {
            foreach (var to in modules.Where(m => m != from))
            {
                var result = Types.InAssembly(Assemblies.Features).That().ResideInNamespace(Root + from)
                    .ShouldNot().HaveDependencyOnAny(Root + to).GetResult();
                if (!result.IsSuccessful) edges.Add($"{from} -> {to}");
            }
        }
        return edges;
    }
}
