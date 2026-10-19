namespace AiNexus.Features.Admin.AccessControl.Navigation;

/// <summary>
/// 完整選單樹上的一個分群與放在其中的功能（<see cref="NavigationTree.Sections"/>），給授權清單照側欄的位置分組。
/// <c>Id</c> 是分群節點 ID（最上層為空字串），<c>Path</c> 是從最上層到它的分群名稱，<c>FeatureIds</c> 依選單順序。
/// </summary>
public sealed record NavigationSectionDto(string Id, IReadOnlyList<string> Path, IReadOnlyList<string> FeatureIds);
