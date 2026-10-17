namespace AiNexus.Features.AccessControl;

/// <summary>
/// 一個功能列及其授權：預設由 <see cref="BuiltInAccess.WorkspaceGroup"/> 授權；若為 <c>AdministratorsOnly</c> 則改由
/// <see cref="BuiltInAccess.AdministratorsGroup"/> 授權。<c>GroupId</c> 是預設選單分群（null 為最上層），<c>Icon</c> 是前端圖示名稱；
/// 管理者之後仍可調整位置與名稱。
/// </summary>
public sealed record PlatformFeature(string Id, string Name, string Route, int SortOrder, string? GroupId, string Icon, bool AdministratorsOnly = false);
