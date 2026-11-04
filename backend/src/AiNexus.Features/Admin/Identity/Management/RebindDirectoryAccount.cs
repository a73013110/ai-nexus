using AiNexus.Data.App;
using AiNexus.Data.App.Identity;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity.Provisioning;
using AiNexus.Features.Admin.Identity.Users;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Identity.Management;

/// <summary>要改綁的 AD 短帳號（<c>sAMAccountName</c>，不含網域）。</summary>
public sealed record DirectoryBindingRequest(string AdAccount, int ProfileVersion);

internal sealed class DirectoryBindingRequestValidator : RequestValidator<DirectoryBindingRequest>
{
    public override Error Problem => IdentityErrors.InvalidAccount;

    public DirectoryBindingRequestValidator() => RuleFor(x => x.AdAccount)
        .NotEmpty()
        .Must(UserAccounts.IsValid).WithErrorCode(RuleCode.Format);
}

/// <summary>
/// 把使用者改綁到另一個 AD 帳號（ADR 0019：換 AD 帳號不算離職）。以 <see cref="IAdDirectory"/> 依短帳號查出 SID 後直接綁定，
/// UserId、角色、部門與歷史資料沿用，不另建使用者。同時寫入新的 <c>AdAccount</c> 並開啟 AD 驗證；姓名與信箱留給「從 AD 更新」。
/// SID 已綁定其他使用者（含已刪除者）或帳號名已被他人使用時拒絕，避免接管別人的身分。遞增 <c>SecurityVersion</c>，舊工作階段失效。
/// </summary>
internal sealed class RebindDirectoryAccount(AppDbContext db, AdministrativeWrite write, IAdDirectory directory) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapPut("/users/{id:guid}/ad-binding", (Guid id, DirectoryBindingRequest body, ICurrentUser user, RebindDirectoryAccount handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, id, body, ct))
        .Audited(IdentityAudit.DirectoryBound)
        .WithName("RebindUserDirectoryAccount");

    public async Task<Result> HandleAsync(Guid actor, Guid id, DirectoryBindingRequest request, CancellationToken ct)
    {
        // 換掉自己的 SID 會讓自己目前的工作階段失效，交由另一位管理員處理。
        if (id == actor) return AccessControlErrors.Lockout;
        // 目錄查詢在鎖與交易外：AD 慢或連不上時不擋住其他管理操作與登入；連不上時拋出 directory_unavailable。
        var person = await directory.FindAccountAsync(request.AdAccount.Trim(), ct);
        if (person is null) return IdentityErrors.DirectoryPersonNotFound;
        var adAccount = UserAccounts.Normalize(person.AccountName)!;

        // SID 與帳號名不可被別人佔用是跨列規則，與登入時的綁定共用授權鎖。
        var begun = await write.BeginAsync(new AuditTarget(id, person.Sid, token => SnapshotAsync(db, id, token)).Locking(AccessControlLocks.Authorization), ct);
        if (!begun.IsSuccess) return begun.Error;
        await using var transaction = begun.Value;

        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);
        if (user is null) return IdentityErrors.UserNotFound;
        if (await db.Users.AnyAsync(x => x.Id != id && x.Sid == person.Sid, ct)) return IdentityErrors.AdBindingTaken;
        if (await db.Users.AnyAsync(x => x.Id != id && x.AdAccount == adAccount, ct)) return IdentityErrors.AccountExists;
        db.ExpectProfileVersion(user, request.ProfileVersion);
        if (user.Sid != person.Sid || user.AdAccount != adAccount || !user.AdEnabled)
        {
            user.Sid = person.Sid;
            user.AdAccount = adAccount;
            user.AdEnabled = true;
            user.SecurityVersion++;
        }

        return await transaction.CommitAsync(ct);
    }

    /// <summary>改綁與解除綁定共用的稽核快照（<c>admin.user_ad_bind</c>、<c>admin.user_ad_unbind</c>）：SID 記完整值，舊 SID 才查得回來。</summary>
    public static async Task<object?> SnapshotAsync(AppDbContext db, Guid id, CancellationToken ct)
        => await db.Users.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new { x.Sid, x.AdAccount, x.AdEnabled, x.SecurityVersion }).SingleOrDefaultAsync(ct);
}
