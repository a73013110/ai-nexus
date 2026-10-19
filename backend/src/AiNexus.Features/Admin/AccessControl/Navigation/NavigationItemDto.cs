using AiNexus.Data.App.AccessControl;

namespace AiNexus.Features.Admin.AccessControl.Navigation;

/// <summary>
/// 一個選單節點的儲存內容（管理端的平面清單，也是 <see cref="NavigationTree.Build"/> 的輸入）。<c>Kind</c> 是 <c>group</c>、<c>page</c>、<c>link</c>；
/// page 的 <c>Name</c>、<c>Icon</c> 為 null（取功能名稱與目錄圖示）；<c>AudienceGroupIds</c> 只有 link 會有，空清單＝所有登入者可見。
/// </summary>
public sealed record NavigationItemDto(string Id, string? ParentId, string Kind, string? FeatureId, string? Name, string? Url, bool OpenInNewTab, string? Icon,
    int SortOrder, IReadOnlyList<string> AudienceGroupIds)
{
    public static NavigationItemDto From(NavigationItem row, IReadOnlyList<string> audiences)
        => new(row.Id, row.ParentId, row.Kind, row.FeatureId, row.Name, row.Url, row.OpenInNewTab, row.Icon, row.SortOrder, audiences);
}
