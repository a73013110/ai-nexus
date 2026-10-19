namespace AiNexus.Features.Admin.AccessControl.Scopes;

/// <summary>一種資料範圍：代碼存在 <c>accesscontrol.RoleGroupFeatures.Scope</c>，名稱給管理介面顯示。</summary>
public sealed record DataScopeDefinition(string Id, string Name);

/// <summary>
/// 資料範圍代碼（ADR 0017）。授權是（群組, 功能, 範圍），一筆授權一個範圍；功能在 <see cref="FeatureCatalog"/> 宣告支援哪些，
/// 宣告了就表示該功能的查詢會套用它（<see cref="IDataScope"/>），所以只在實際套用的功能上宣告。
/// 資料庫的 <c>ck_RoleGroupFeatures_Scope</c> 列出同一組代碼。
/// </summary>
public static class DataScopes
{
    public const string Own = "own";
    public const string Assigned = "assigned";
    public const string Unit = "unit";
    public const string UnitTree = "unit-tree";
    public const string All = "all";

    /// <summary>由窄到寬；目錄宣告、OpenAPI 與管理介面的選項都照這個順序。</summary>
    public static IReadOnlyList<DataScopeDefinition> Definitions { get; } =
    [
        new(Own, "本人"),
        new(Assigned, "被指派"),
        new(Unit, "所屬單位"),
        new(UnitTree, "所屬單位及下層"),
        new(All, "全部"),
    ];
}
