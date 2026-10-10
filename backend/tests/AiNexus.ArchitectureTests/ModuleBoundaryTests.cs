using System.Reflection;
using AiNexus.Platform.Errors;
using NetArchTest.Rules;

namespace AiNexus.ArchitectureTests;

/// <summary>
/// Modules may depend on each other, but never in a cycle: the dependency graph must stay layered so each module can be
/// read, tested and changed with only the modules below it in mind. To break a new cycle, move the type to the module
/// that owns the behavior, let the lower module define an interface the upper one implements, or publish a domain event.
/// </summary>
public sealed class ModuleBoundaryTests
{
    private const string Root = "AiNexus.Features.";

    // Shared infrastructure of the Features assembly, not business modules.
    private static readonly HashSet<string> Infrastructure = ["Persistence"];

    [Fact]
    public void ModuleDependenciesHaveNoCycles()
    {
        var edges = Edges();
        var cycles = Components(edges).Where(x => x.Count > 1).Select(x => Describe(Cycle(x, edges), edges)).ToList();
        Assert.True(cycles.Count == 0, "Module dependency cycles (each line: the types that create the dependency):\n" + string.Join("\n\n", cycles));
    }

    /// <summary>A module is a folder of the Features project; its root holds <c>&lt;Module&gt;Module.cs</c>, the one place it is registered.</summary>
    [Fact]
    public void EveryModuleHasAModuleClassAtItsRoot()
    {
        var folders = Directory.EnumerateDirectories(Path.Combine(SourceTree.Root, "backend", "src", "AiNexus.Features"))
            .Select(Path.GetFileName).OfType<string>().Where(x => x is not ("bin" or "obj") && !Infrastructure.Contains(x)).ToList();
        Assert.NotEmpty(folders);
        var missing = folders.Where(module => Assemblies.Features.GetType(Root + module + "." + module + "Module") is null
            || !File.Exists(Path.Combine(SourceTree.Root, "backend", "src", "AiNexus.Features", module, module + "Module.cs"))).ToList();
        Assert.True(missing.Count == 0, "Modules without <Module>/<Module>Module.cs declaring <Module>Module: " + string.Join(", ", missing));
    }

    /// <summary>A module's expected failures are listed in one place, <c>&lt;Module&gt;Errors</c> at the module root.</summary>
    [Fact]
    public void ErrorsAreDeclaredInTheModuleErrorsClass()
    {
        var misplaced = Assemblies.Features.GetTypes()
            .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(f => f.FieldType == typeof(Error) && f.DeclaringType!.Namespace?.StartsWith(Root, StringComparison.Ordinal) == true)
            .Where(f =>
            {
                var module = f.DeclaringType!.Namespace![Root.Length..].Split('.')[0];
                return f.DeclaringType.FullName != Root + module + "." + module + "Errors";
            })
            .Select(f => f.DeclaringType!.FullName + "." + f.Name).Order(StringComparer.Ordinal).ToList();
        Assert.True(misplaced.Count == 0, "Move these errors to <Module>Errors:\n" + string.Join("\n", misplaced));
    }

    /// <summary>Each module-to-module dependency with the types of the depending module that cause it.</summary>
    internal static Dictionary<(string From, string To), IReadOnlyList<string>> Edges()
    {
        var modules = Assemblies.Features.GetTypes()
            .Select(t => t.Namespace).OfType<string>().Where(ns => ns.StartsWith(Root, StringComparison.Ordinal))
            .Select(ns => ns[Root.Length..].Split('.')[0]).Where(m => !Infrastructure.Contains(m)).ToHashSet(StringComparer.Ordinal);
        var edges = new Dictionary<(string, string), IReadOnlyList<string>>();
        // Read the assembly once; every pair query filters the same type list.
        var types = Types.InAssembly(Assemblies.Features);
        foreach (var from in modules)
        {
            foreach (var to in modules.Where(m => m != from))
            {
                var result = types.That().ResideInNamespace(Root + from)
                    .ShouldNot().HaveDependencyOnAny(Root + to).GetResult();
                if (!result.IsSuccessful) edges[(from, to)] = result.FailingTypes?.Select(t => TopLevelName(t.FullName)).Distinct().Order(StringComparer.Ordinal).ToList() ?? [];
            }
        }
        return edges;
    }

    // Compiler-generated lambdas and state machines are reported as the type that declares them.
    private static string TopLevelName(string fullName)
    {
        var type = fullName.Split('/', '+')[0];
        return type[(type.LastIndexOf('.') + 1)..];
    }

    // Tarjan's strongly connected components; a component with more than one module contains a cycle.
    private static List<List<string>> Components(Dictionary<(string From, string To), IReadOnlyList<string>> edges)
    {
        var next = edges.Keys.GroupBy(x => x.From).ToDictionary(g => g.Key, g => g.Select(x => x.To).Order(StringComparer.Ordinal).ToList());
        var nodes = edges.Keys.SelectMany(x => new[] { x.From, x.To }).Distinct().Order(StringComparer.Ordinal);
        var index = new Dictionary<string, int>(); var low = new Dictionary<string, int>();
        var stack = new Stack<string>(); var onStack = new HashSet<string>(); var components = new List<List<string>>();
        foreach (var node in nodes) if (!index.ContainsKey(node)) Visit(node);
        return components;

        void Visit(string node)
        {
            index[node] = low[node] = index.Count;
            stack.Push(node); onStack.Add(node);
            foreach (var to in next.GetValueOrDefault(node) ?? [])
            {
                if (!index.TryGetValue(to, out var visited)) { Visit(to); low[node] = Math.Min(low[node], low[to]); }
                else if (onStack.Contains(to)) low[node] = Math.Min(low[node], visited);
            }
            if (low[node] != index[node]) return;
            var component = new List<string>();
            string member;
            do { member = stack.Pop(); onStack.Remove(member); component.Add(member); } while (member != node);
            components.Add(component);
        }
    }

    // A shortest cycle through the component's first module, found breadth-first inside the component.
    private static List<string> Cycle(List<string> component, Dictionary<(string From, string To), IReadOnlyList<string>> edges)
    {
        var members = component.ToHashSet(StringComparer.Ordinal);
        var start = component.Order(StringComparer.Ordinal).First();
        var previous = new Dictionary<string, string>();
        var queue = new Queue<string>([start]);
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            foreach (var to in edges.Keys.Where(x => x.From == node && members.Contains(x.To)).Select(x => x.To).Order(StringComparer.Ordinal))
            {
                if (to == start)
                {
                    var path = new List<string> { start };
                    for (var at = node; at != start; at = previous[at]) path.Insert(1, at);
                    path.Add(start);
                    return path;
                }
                if (previous.TryAdd(to, node)) queue.Enqueue(to);
            }
        }
        return [start];
    }

    private static string Describe(List<string> cycle, Dictionary<(string From, string To), IReadOnlyList<string>> edges)
        => string.Join(" -> ", cycle) + "\n" + string.Join("\n", cycle.Zip(cycle.Skip(1))
            .Select(x => $"  {x.First} -> {x.Second}: {string.Join(", ", edges[(x.First, x.Second)])}"));
}
