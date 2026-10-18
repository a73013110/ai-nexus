namespace AiNexus.Features.Admin.AccessControl.Navigation;

/// <summary>選單分群。<c>ParentId</c> 為 null 代表最上層；同一層中功能排在分群之前，兩者各自依 <c>SortOrder</c>、再依 Id 排序。</summary>
public sealed record NavigationGroupDto(string Id, string? ParentId, string Name, int SortOrder);
