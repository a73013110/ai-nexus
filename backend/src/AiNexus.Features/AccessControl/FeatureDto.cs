namespace AiNexus.Features.AccessControl;

/// <summary>選單所見的功能；<c>GroupId</c> 為 null 代表頂層，<c>Route</c> 為空代表純授權、沒有頁面。</summary>
public sealed record FeatureDto(string Id, string Name, string Route, string? GroupId, string Icon, int SortOrder);
