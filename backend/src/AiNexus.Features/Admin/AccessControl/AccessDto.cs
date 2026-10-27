using System.Text.Json.Serialization;
using AiNexus.Features.Admin.AccessControl.Navigation;

namespace AiNexus.Features.Admin.AccessControl;

public sealed record AccessItemDto(string Id, string Name);

/// <summary>
/// 有效授權。<c>Navigation</c>（已修剪、攤平的選單樹）與 <see cref="DataScopes"/> 只有在 <see cref="AccessView"/>（繪製畫面的 payload）才會填入；
/// 授權檢查不讀它們，因此省下選單與單位查詢。
/// </summary>
public sealed record AccessDto(IReadOnlyList<AccessItemDto> Roles, IReadOnlyList<AccessItemDto> Groups, IReadOnlyList<FeatureDto> Features, IReadOnlyList<NavigationNodeDto> Navigation)
{
    /// <summary>
    /// 已授權且目錄宣告了範圍的功能 → 各群組授權的成員範圍與主管範圍的聯集（只留目錄支援的代碼；主管範圍還沒確認此人任主管）。
    /// 給 <c>IDataScope</c> 用，與功能授權同一次讀取。
    /// </summary>
    [JsonIgnore]
    public IReadOnlyDictionary<string, IReadOnlySet<string>> GrantedScopes { get; init; } = new Dictionary<string, IReadOnlySet<string>>();

    /// <summary>
    /// 功能 → 有效範圍代碼（含此人實際任主管時的主管範圍，由窄到寬），只列有範圍的功能；同 <c>IDataScope.ScopesAsync</c>。
    /// 前端只拿來決定版面（例如只看得到本人時不顯示選人），資料仍由各查詢依範圍篩選。
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> DataScopes { get; init; } = new Dictionary<string, IReadOnlyList<string>>();
}
