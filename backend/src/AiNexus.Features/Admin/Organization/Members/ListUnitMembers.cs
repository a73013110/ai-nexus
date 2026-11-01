using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Organization.Members;

/// <summary>單位的一位直屬成員。<c>Active</c> 為 false 表示帳號已停用或刪除：所屬保留到管理員移除，但不參與資料範圍。</summary>
public sealed record UnitMemberDto(Guid UserId, string Account, string DisplayName, bool Active, bool IsPrimary, bool IsManager);

/// <summary>單位的直屬成員（不含下層單位），主管在前，再依帳號排列。</summary>
internal sealed class ListUnitMembers(AppDbContext db, IActiveUsers activeUsers)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("/{id:guid}/members", (Guid id, ListUnitMembers handler, CancellationToken ct) =>
            handler.HandleAsync(id, ct))
        .Audited(OrganizationAudit.MemberRead)
        .WithName("ListOrganizationUnitMembers");

    public async Task<Result<UnitMemberDto[]>> HandleAsync(Guid id, CancellationToken ct)
    {
        if (!await db.Units.AnyAsync(x => x.Id == id, ct)) return OrganizationErrors.UnitNotFound;
        return await db.UnitMembers.AsNoTracking().Where(x => x.UnitId == id)
            .Join(db.Users, x => x.UserId, x => x.Id, (member, user) => new { member, user })
            .OrderByDescending(x => x.member.IsManager).ThenBy(x => x.user.AdAccount ?? x.user.LocalAccount)
            .Select(x => new UnitMemberDto(x.user.Id, x.user.AdAccount ?? x.user.LocalAccount ?? "", x.user.DisplayName, activeUsers.Ids.Contains(x.user.Id), x.member.IsPrimary, x.member.IsManager))
            .ToArrayAsync(ct);
    }
}
