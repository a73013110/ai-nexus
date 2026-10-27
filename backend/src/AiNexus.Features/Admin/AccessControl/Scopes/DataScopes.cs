namespace AiNexus.Features.Admin.AccessControl.Scopes;

/// <summary>範圍依據的單位：不看單位（<c>own</c>、<c>assigned</c>、<c>all</c>）、使用者所屬的單位，或其任主管的單位。</summary>
public enum DataScopeUnits
{
    None,
    Member,
    Managed,
}

/// <summary>
/// 一種資料範圍：代碼存在授權上，名稱給管理介面顯示。<see cref="Units"/> 與 <see cref="Subtree"/> 決定 <see cref="IDataScope"/> 要算哪些單位，
/// 所以新增依單位的範圍只要在這裡宣告，不必改計算。
/// </summary>
public sealed record DataScopeDefinition(string Id, string Name, DataScopeUnits Units = DataScopeUnits.None, bool Subtree = false);

/// <summary>
/// 資料範圍代碼（ADR 0017、0022）。授權是（群組, 功能, 範圍, 主管範圍）：<see cref="Member"/> 給群組的所有成員，<see cref="Manager"/> 是成員中
/// 組織勾選為主管的人另可看的，兩者以 OR 合併。功能在 <see cref="FeatureCatalog"/> 宣告各支援哪些，宣告了就表示該功能的查詢會套用它（<see cref="IDataScope"/>），
/// 所以只在實際套用的功能上宣告。資料庫的 <c>ck_RoleGroupFeatures_Scope</c>、<c>ck_RoleGroupFeatures_ManagerScope</c> 各列出同一組代碼。
/// </summary>
public static class DataScopes
{
    public const string Own = "own";
    public const string Assigned = "assigned";
    public const string Unit = "unit";
    public const string UnitTree = "unit-tree";
    public const string All = "all";
    public const string Managed = "managed";
    public const string ManagedTree = "managed-tree";

    /// <summary>所有成員的範圍（<c>RoleGroupFeatures.Scope</c>），由窄到寬。</summary>
    public static IReadOnlyList<DataScopeDefinition> Member { get; } =
    [
        new(Own, "本人"),
        new(Assigned, "被指派"),
        new(Unit, "所屬單位", DataScopeUnits.Member),
        new(UnitTree, "所屬單位及下層", DataScopeUnits.Member, Subtree: true),
        new(All, "全部"),
    ];

    /// <summary>主管另可看的範圍（<c>RoleGroupFeatures.ManagerScope</c>），由窄到寬；只算任主管的單位，所以全是依單位的範圍。</summary>
    public static IReadOnlyList<DataScopeDefinition> Manager { get; } =
    [
        new(Managed, "所管單位", DataScopeUnits.Managed),
        new(ManagedTree, "所管單位及下層", DataScopeUnits.Managed, Subtree: true),
    ];

    /// <summary>兩組依序相接（代碼不重複）；有效範圍、OpenAPI 與畫面的名稱都照這個順序。</summary>
    public static IReadOnlyList<DataScopeDefinition> Definitions { get; } = [.. Member, .. Manager];

    private static readonly Dictionary<string, DataScopeDefinition> ById = Definitions.ToDictionary(x => x.Id, StringComparer.Ordinal);

    public static DataScopeDefinition? Find(string id) => ById.GetValueOrDefault(id);

    /// <summary>依 <see cref="Definitions"/> 的順序排列代碼，不認得的捨去。</summary>
    public static IEnumerable<DataScopeDefinition> InOrder(IEnumerable<string> ids)
    {
        var set = ids.ToHashSet(StringComparer.Ordinal);
        return Definitions.Where(x => set.Contains(x.Id));
    }
}
