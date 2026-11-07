using AiNexus.Platform.Auditing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNexus.ArchitectureTests;

/// <summary>
/// 稽核明細寫入時只保留 <see cref="AuditRedactor"/> 白名單裡的鍵，其他鍵默默丟掉（遮罩優先，寫入不因此失敗）。
/// 所以新增明細忘了加白名單，看起來有記、實際沒記。這裡掃 <c>Describe</c>／<c>Record</c> 寫在呼叫處的匿名物件（含巢狀），
/// 每個鍵都要在白名單內；先組好再傳入的明細掃不到，所以明細一律寫在呼叫處。
/// </summary>
public sealed class AuditDetailTests
{
    // details 的位置：Describe(resource, subject, details)、Record(action, owner, resource, subject, details, result)。
    private static readonly Dictionary<string, int> DetailsPosition = new(StringComparer.Ordinal) { ["Describe"] = 2, ["Record"] = 4 };

    [Fact]
    public void AuditDetailKeysAreAllowedByTheRedactor()
    {
        var dropped = new[] { "AiNexus.Features", "AiNexus.Data" }
            .SelectMany(SourceTree.Files)
            .SelectMany(path => DetailKeys(path).Where(x => !AuditRedactor.Allows(x.Key))
                .Select(x => $"{SourceTree.Relative(path)}:{x.Line} {x.Key}"))
            .ToList();
        Assert.True(dropped.Count == 0, "These audit detail keys are not in AuditRedactor's allow list and would be dropped:\n" + string.Join('\n', dropped));
    }

    [Fact]
    public void TheScanSeesDetailKeys()
    {
        // 掃描本身失效（例如方法改名）時上一個測試會空轉通過，所以確認它看得到已知的明細。
        var keys = SourceTree.Files("AiNexus.Features").SelectMany(DetailKeys).Select(x => x.Key).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("automatic", keys);
        Assert.Contains("fileId", keys);
    }

    private static IEnumerable<(string Key, int Line)> DetailKeys(string path)
    {
        var root = CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetRoot();
        foreach (var call in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (call.Expression is not MemberAccessExpressionSyntax { Name.Identifier.Text: var method } || !DetailsPosition.TryGetValue(method, out var position)) continue;
            var arguments = call.ArgumentList.Arguments;
            var details = arguments.FirstOrDefault(x => x.NameColon?.Name.Identifier.Text == "details")
                ?? (arguments.Count > position && arguments[position].NameColon is null ? arguments[position] : null);
            if (details?.Expression is not AnonymousObjectCreationExpressionSyntax anonymous) continue;
            foreach (var member in anonymous.DescendantNodesAndSelf().OfType<AnonymousObjectCreationExpressionSyntax>().SelectMany(x => x.Initializers))
                if (Name(member) is { } key)
                    yield return (char.ToLowerInvariant(key[0]) + key[1..], member.GetLocation().GetLineSpan().StartLinePosition.Line + 1);
        }
    }

    /// <summary>匿名物件成員的名稱：<c>key = …</c> 或由投影推得（<c>count</c>、<c>x.Id</c>）。</summary>
    private static string? Name(AnonymousObjectMemberDeclaratorSyntax member) => member.NameEquals?.Name.Identifier.Text ?? member.Expression switch
    {
        IdentifierNameSyntax name => name.Identifier.Text,
        MemberAccessExpressionSyntax access => access.Name.Identifier.Text,
        ConditionalAccessExpressionSyntax { WhenNotNull: MemberBindingExpressionSyntax binding } => binding.Name.Identifier.Text,
        _ => null,
    };
}
