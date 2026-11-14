using AiNexus.Platform.Modules;

namespace AiNexus.Features.Office.Flow;

/// <summary>已註冊的申請種類與審核人規則（宣告，不含實作）；設定畫面、送出檢查與種子測試共用。</summary>
[Singleton]
public sealed class FlowCatalog(IEnumerable<FlowKindDefinition> kinds, IEnumerable<ApproverRuleDefinition> rules)
{
    private readonly Dictionary<string, FlowKindDefinition> kindsByKey = kinds.ToDictionary(x => x.Kind, StringComparer.Ordinal);
    private readonly Dictionary<string, ApproverRuleDefinition> rulesByKey = rules.ToDictionary(x => x.Key, StringComparer.Ordinal);

    public IReadOnlyList<FlowKindDefinition> Kinds => [.. kindsByKey.Values.OrderBy(x => x.Kind, StringComparer.Ordinal)];

    public IReadOnlyList<ApproverRuleDefinition> Rules => [.. rulesByKey.Values.OrderBy(x => x.Key, StringComparer.Ordinal)];

    public FlowKindDefinition? Kind(string kind) => kindsByKey.GetValueOrDefault(kind);

    public ApproverRuleDefinition? Rule(string key) => rulesByKey.GetValueOrDefault(key);

    /// <summary>種類的顯示名稱；種類已移除（舊申請）時顯示鍵本身。</summary>
    public string KindName(string kind) => Kind(kind)?.Name ?? kind;
}
