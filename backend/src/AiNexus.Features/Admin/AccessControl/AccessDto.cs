using System.Text.Json.Serialization;
using AiNexus.Features.Admin.AccessControl.Navigation;

namespace AiNexus.Features.Admin.AccessControl;

public sealed record AccessItemDto(string Id, string Name);

/// <summary>
/// 有效授權。<c>Navigation</c>（已修剪、攤平的選單樹）只有在 <see cref="AccessService.WithNavigationAsync"/>（繪製選單的 payload）才會填入；
/// 授權檢查不讀它，因此省下選單查詢。
/// </summary>
public sealed record AccessDto(IReadOnlyList<AccessItemDto> Roles, IReadOnlyList<AccessItemDto> Groups, IReadOnlyList<FeatureDto> Features, IReadOnlyList<NavigationNodeDto> Navigation)
{
    /// <summary>
    /// 已授權且目錄宣告了範圍的功能 → 各群組授權範圍的聯集（只留目錄支援的代碼）。給 <c>IDataScope</c> 用，與功能授權同一次讀取；
    /// 不輸出給前端：範圍只影響伺服器查得到哪些資料。
    /// </summary>
    [JsonIgnore]
    public IReadOnlyDictionary<string, IReadOnlySet<string>> Scopes { get; init; } = new Dictionary<string, IReadOnlySet<string>>();
}
