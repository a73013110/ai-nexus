using AiNexus.Data.App.Organization;

namespace AiNexus.Features.Admin.Organization;

/// <summary>管理端的一個單位（平面清單，前端依 <c>ParentId</c> 組回樹）。<c>MemberCount</c> 只算直屬成員，不含下層。</summary>
public sealed record UnitDto(Guid Id, Guid? ParentId, string Name, bool Enabled, int SortOrder, int MemberCount)
{
    public static UnitDto From(Unit row, int members) => new(row.Id, row.ParentId, row.Name, row.Enabled, row.SortOrder, members);
}
