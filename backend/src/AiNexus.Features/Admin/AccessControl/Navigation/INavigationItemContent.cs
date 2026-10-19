namespace AiNexus.Features.Admin.AccessControl.Navigation;

/// <summary>新增與修改選單節點共用的內容欄位，讓兩個 request 由同一份 <see cref="NavigationItemContentValidator"/> 檢查。</summary>
public interface INavigationItemContent
{
    string Kind { get; }
    string? FeatureId { get; }
    string? Name { get; }
    string? Url { get; }
    string? Icon { get; }
    IReadOnlyList<string>? AudienceGroupIds { get; }
}
