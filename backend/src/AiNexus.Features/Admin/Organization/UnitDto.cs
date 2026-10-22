using AiNexus.Data.App.Organization;

namespace AiNexus.Features.Admin.Organization;

/// <summary>
/// 管理端的一個單位（平面清單，前端依 <c>ParentId</c> 組回樹）。<c>MemberCount</c> 只算直屬成員，不含下層。
/// <c>GdDepNo</c> 有值表示來自公文系統：名稱、上層、啟用、順序與主管在下次匯入時以 GD 為準。
/// </summary>
public sealed record UnitDto(Guid Id, Guid? ParentId, string Name, bool Enabled, int SortOrder, int MemberCount, int? GdDepNo)
{
    public static UnitDto From(Unit row, int members) => new(row.Id, row.ParentId, row.Name, row.Enabled, row.SortOrder, members, row.GdDepNo);
}
