using System.Text.RegularExpressions;
using AiNexus.IntegrationTests.Host;

namespace AiNexus.IntegrationTests.Data;

/// <summary>
/// procedure 回傳的欄位與 DTO 只在執行時對應（ADR 0011）：程式呼叫的每支 procedure 都要有整合測試在真實資料庫上執行過。
/// </summary>
public sealed partial class ProcedureCoverageTests
{
    [Fact]
    public void EveryProcedureCalledByCodeAppearsInAnIntegrationTest()
    {
        var root = OpenApiContractTests.RepositoryRoot();
        static IEnumerable<string> Names(string folder, params string[] patterns) => patterns
            .SelectMany(pattern => Directory.EnumerateFiles(folder, pattern, SearchOption.AllDirectories))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(path => Procedure().Matches(File.ReadAllText(path)).Select(match => match.Value));
        var called = Names(Path.Combine(root, "backend", "src"), "*.cs", "*.sql").ToHashSet(StringComparer.OrdinalIgnoreCase);
        var tested = Names(Path.Combine(root, "backend", "tests", "AiNexus.IntegrationTests"), "*.cs").ToHashSet(StringComparer.OrdinalIgnoreCase);
        var untested = called.Except(tested, StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal).ToList();
        Assert.True(untested.Count == 0, "為這些 procedure 加整合測試（在 LocalDB 上實際呼叫並檢查回傳欄位）：\n" + string.Join("\n", untested));
    }

    // procedure 名稱＝usp_＋use case 名稱（ADR 0011），程式與測試以名稱引用。
    [GeneratedRegex(@"\busp_\w+")]
    private static partial Regex Procedure();
}
