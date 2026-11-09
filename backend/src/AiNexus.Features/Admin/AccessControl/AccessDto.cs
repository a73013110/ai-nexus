using AiNexus.Features.Admin.AccessControl.Navigation;

namespace AiNexus.Features.Admin.AccessControl;

public sealed record AccessItemDto(string Id, string Name);

/// <summary>
/// 繪製畫面用的授權（<c>/me</c> 與存取預覽），由 <see cref="AccessView"/> 組成：有效授權再加上角色、已修剪攤平的選單樹與資料範圍。
/// 授權判斷讀 <see cref="EffectiveAccess"/>，省下這些查詢。
/// </summary>
public sealed record AccessDto(IReadOnlyList<AccessItemDto> Roles, IReadOnlyList<AccessItemDto> Groups, IReadOnlyList<FeatureDto> Features, IReadOnlyList<NavigationNodeDto> Navigation)
{
    /// <summary>
    /// 功能 → 有效範圍代碼（含此人實際任主管時的主管範圍，由窄到寬），只列有範圍的功能；同 <c>IDataScope.ScopesAsync</c>。
    /// 前端只拿來決定版面（例如只看得到本人時不顯示選人），資料仍由各查詢依範圍篩選。
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> DataScopes { get; init; } = new Dictionary<string, IReadOnlyList<string>>();
}
