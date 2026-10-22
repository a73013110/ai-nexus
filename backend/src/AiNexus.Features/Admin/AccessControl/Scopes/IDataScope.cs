namespace AiNexus.Features.Admin.AccessControl.Scopes;

/// <summary>
/// 使用者對某個功能的有效資料範圍：各有效群組授權的範圍以 OR 合併。模組自己把它轉成查詢條件（EF 的 Where 或預存程序參數），
/// 不用 EF 全域查詢篩選（ADR 0017）。<see cref="IsEmpty"/> 時沒有資料；單筆讀取查不到一律回 404，不透露資料存在。
/// </summary>
/// <param name="Scopes">
/// 授權的範圍代碼（<see cref="DataScopes"/>），只含功能目錄宣告支援的；使用者任主管且功能宣告了 <see cref="FeatureDefinition.ManagerScope"/> 時也含它。
/// </param>
/// <param name="UnitIds">
/// <c>unit</c> 是使用者目前所屬的有效單位，<c>unit-tree</c> 再加上它們所有的有效下層；主管範圍是任主管的有效單位（<c>unit-tree</c> 時含下層）。
/// 全部取聯集；有 <c>all</c> 時不計算（為空）。
/// 業務資料若在寫入時存了單位 ID，直接比對它；沒有存的，再由 Organization 的 public 服務換成目前的成員。
/// </param>
public sealed record EffectiveDataScope(IReadOnlySet<string> Scopes, IReadOnlySet<Guid> UnitIds)
{
    public static EffectiveDataScope None { get; } = new(new HashSet<string>(), new HashSet<Guid>());

    public bool IsEmpty => Scopes.Count == 0;
    public bool All => Scopes.Contains(DataScopes.All);
    public bool Own => Scopes.Contains(DataScopes.Own);

    /// <summary><c>assigned</c> 的關係由業務模組擁有並自行判斷；平台只回答有沒有這個範圍。</summary>
    public bool Assigned => Scopes.Contains(DataScopes.Assigned);
}

/// <summary>
/// 平台提供的資料範圍查詢（ADR 0017）。與功能授權共用 <see cref="AccessService.ForUserAsync"/> 的同一次讀取，
/// 所以同一個請求內，policy 檢查之後再取範圍不會多讀授權。
/// </summary>
public interface IDataScope
{
    /// <summary>
    /// 使用者對 <paramref name="featureId"/> 的有效範圍。沒有授權、功能停用，或授權沒有可用範圍又沒有主管範圍時回 <see cref="EffectiveDataScope.None"/>（失敗即拒絕）。
    /// 功能在目錄沒有宣告範圍是程式錯誤，直接拋出：宣告範圍才代表管理介面能設定它。
    /// </summary>
    Task<EffectiveDataScope> ForAsync(Guid userId, string featureId, CancellationToken ct);
}
