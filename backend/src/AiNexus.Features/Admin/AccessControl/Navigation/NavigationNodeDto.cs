namespace AiNexus.Features.Admin.AccessControl.Navigation;

/// <summary>
/// 使用者看得到的選單樹的一個節點（已修剪、攤平）。page 有 <c>FeatureId</c> 與 <c>Route</c>，名稱是功能的有效名稱、圖示來自目錄；
/// link 有 <c>Url</c> 與 <c>OpenInNewTab</c>；group 有 <c>Children</c>。其他種類的 <c>OpenInNewTab</c> 一律為 false。
/// </summary>
public sealed record NavigationNodeDto(string Id, string Kind, string Name, string? Icon, string? FeatureId, string? Route, string? Url, bool OpenInNewTab,
    IReadOnlyList<NavigationNodeDto> Children);
