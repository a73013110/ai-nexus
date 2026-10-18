using System.Reflection;
using AiNexus.Platform.Errors;
using NetArchTest.Rules;

namespace AiNexus.ArchitectureTests;

/// <summary>
/// 模組之間可以互相依賴，但不可形成循環，依賴圖必須維持分層，讓每個模組只需考慮下層模組即可閱讀與修改。
/// 要打破循環，可以把型別搬到真正擁有該行為的模組、由下層定義介面讓上層實作，或改用領域事件（domain event）。
/// </summary>
public sealed class ModuleBoundaryTests
{
    private const string Root = "AiNexus.Features.";

    [Fact]
    public void ModuleDependenciesHaveNoCycles()
    {
        var edges = Edges();
        var cycles = Components(edges).Where(x => x.Count > 1).Select(x => Describe(Cycle(x, edges), edges)).ToList();
        Assert.True(cycles.Count == 0, "Module dependency cycles (each line: the types that create the dependency):\n" + string.Join("\n\n", cycles));
    }

    /// <summary>模組就是 Features 專案下 <c>&lt;Area&gt;/&lt;Module&gt;</c> 的資料夾；根目錄的 <c>&lt;Module&gt;Module.cs</c> 是唯一註冊模組的地方。</summary>
    [Fact]
    public void EveryModuleHasAModuleClassAtItsRoot()
    {
        var folders = SourceTree.ModuleFolders();
        Assert.NotEmpty(folders);
        var missing = folders.Where(x => Assemblies.Features.GetType(Root + x.Area + "." + x.Module + "." + x.Module + "Module") is null
            || !File.Exists(Path.Combine(SourceTree.FeaturesRoot, x.Area, x.Module, x.Module + "Module.cs"))).Select(x => x.Area + "/" + x.Module).ToList();
        Assert.True(missing.Count == 0, "Modules without <Area>/<Module>/<Module>Module.cs declaring <Module>Module: " + string.Join(", ", missing));
    }

    /// <summary>模組預期的錯誤集中定義在模組根目錄的 <c>&lt;Module&gt;Errors</c>。</summary>
    [Fact]
    public void ErrorsAreDeclaredInTheModuleErrorsClass()
    {
        var misplaced = Assemblies.Features.GetTypes()
            .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(f => f.FieldType == typeof(Error) && f.DeclaringType!.Namespace?.StartsWith(Root, StringComparison.Ordinal) == true)
            .Where(f =>
            {
                var module = Module(f.DeclaringType!.Namespace!);
                return f.DeclaringType.FullName != Root + module + "." + module[(module.IndexOf('.', StringComparison.Ordinal) + 1)..] + "Errors";
            })
            .Select(f => f.DeclaringType!.FullName + "." + f.Name).Order(StringComparer.Ordinal).ToList();
        Assert.True(misplaced.Count == 0, "Move these errors to <Module>Errors:\n" + string.Join("\n", misplaced));
    }

    /// <summary>每組模組對模組的依賴，以及造成該依賴的來源模組型別。</summary>
    internal static Dictionary<(string From, string To), IReadOnlyList<string>> Edges()
    {
        var modules = Assemblies.Features.GetTypes()
            .Select(t => t.Namespace).OfType<string>().Where(ns => ns.StartsWith(Root, StringComparison.Ordinal))
            .Select(Module).ToHashSet(StringComparer.Ordinal);
        var edges = new Dictionary<(string, string), IReadOnlyList<string>>();
        // 組件只讀一次；之後每組配對查詢都篩同一份型別清單，避免對每個配對重掃組件。
        var types = Types.InAssembly(Assemblies.Features);
        foreach (var from in modules)
        {
            foreach (var to in modules.Where(m => m != from))
            {
                var result = types.That().ResideInNamespace(Root + from)
                    .ShouldNot().HaveDependencyOnAny(Root + to).GetResult();
                if (result.IsSuccessful)
                {
                    continue;
                }

                edges[(from, to)] = result.FailingTypes?.Select(t => TopLevelName(t.FullName)).Distinct().Order(StringComparer.Ordinal).ToList() ?? [];
            }
        }
        return edges;
    }

    /// <summary>
    /// 模組鍵是 <c>AiNexus.Features</c> 之後的「區.模組」兩段（例如 <c>Admin.AccessControl</c>）。
    /// 區只是資料夾、不是依賴邊界：同區模組之間的依賴與跨區依賴適用同一套無循環規則。
    /// </summary>
    private static string Module(string ns) => string.Join('.', ns[Root.Length..].Split('.').Take(2));

    // 編譯器產生的 lambda 與狀態機會以宣告它們的型別回報（巢狀型別以 '+' 分隔），這裡只取最外層的型別名。
    private static string TopLevelName(string fullName)
    {
        var type = fullName.Split('/', '+')[0];
        return type[(type.LastIndexOf('.') + 1)..];
    }

    // Tarjan 強連通元件演算法；元件內若有超過一個模組，就代表存在循環。
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

    // 在元件內以廣度優先搜尋，找出經過元件第一個模組的最短循環，讓錯誤訊息最短、最易讀。
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
