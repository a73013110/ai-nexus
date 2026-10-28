using AiNexus.Data.App.Organization;

namespace AiNexus.Features.Admin.Organization;

/// <summary>
/// 管理端的一個單位（平面清單，前端依 <c>ParentId</c> 組回樹）。<c>MemberCount</c> 只算直屬成員，不含下層。
/// <c>GdDepNo</c> 有值表示來自公文系統：名稱、上層、啟用、順序與主管在下次匯入時以 GD 為準。
/// <c>HeadRank</c> 有值是上層的首長單位（ADR 0024），本地設定、匯入不覆寫；上層因此不再勾選主管。
/// </summary>
public sealed record UnitDto(Guid Id, Guid? ParentId, string Name, bool Enabled, int SortOrder, int MemberCount, int? GdDepNo, byte? HeadRank)
{
    public static UnitDto From(Unit row, int members) => new(row.Id, row.ParentId, row.Name, row.Enabled, row.SortOrder, members, row.GdDepNo, row.HeadRank);
}
