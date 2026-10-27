using AiNexus.Data.App;
using AiNexus.Data.App.Identity;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity.SelfService;
using AiNexus.Features.Admin.Identity.Users;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Events;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Identity.Management;

/// <summary>管理員設定的通知信箱；空白或 null 表示清除。</summary>
public sealed record UserEmailRequest(string? Email);

internal sealed class UserEmailRequestValidator : RequestValidator<UserEmailRequest>
{
    public override string ProblemCode => "email_invalid";

    public UserEmailRequestValidator() => RuleFor(x => x.Email)
        .Must(x => string.IsNullOrWhiteSpace(x) || UserEmails.TryNormalize(x, out _)).WithErrorCode("format");
}

/// <summary>
/// 管理員變更了使用者的通知信箱。<paramref name="PreviousVerifiedEmail"/> 是變更前已驗證的地址（沒有則 null），
/// 訂閱端據此寄通知（Account 寄到舊信箱）。
/// </summary>
public sealed record UserEmailChanged(Guid UserId, string DisplayName, string? PreviousVerifiedEmail, string? Email, Guid ActorId) : IDomainEvent;

/// <summary>
/// 管理員設定或清除使用者的通知信箱。AD 提供的信箱由目錄同步，這裡拒絕覆寫；設定的地址標記為管理者來源且<b>未驗證</b>：
/// 管理員無法證明使用者持有該信箱，所以步升驗證碼等安全用途仍只用本人驗證過的地址，直到本人在帳號設定完成驗證。
/// 不遞增 <c>SecurityVersion</c>：未驗證的地址不授予任何能力，不必登出使用者；舊的已驗證信箱會收到通知。
/// </summary>
internal sealed class SetUserEmail(AppDbContext db, AdministrativeAudit audit, UserCredentials credentials, DomainEvents events)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPut("/users/{id:guid}/email", (Guid id, UserEmailRequest body, ICurrentUser user, SetUserEmail handler, CancellationToken ct) => handler.HandleAsync(user.Id, id, body, ct).ToHttpResultAsync())
        .Audited(IdentityAudit.UserEmailUpdated)
        .WithName("SetUserEmail");

    public Task<Result> HandleAsync(Guid actor, Guid id, UserEmailRequest request, CancellationToken ct) => audit.MutateAsync(actor, FeatureCatalog.IdentityUsers, id, id.ToString(), token => SnapshotAsync(id, token), async () =>
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);
        if (user is null) return IdentityErrors.UserNotFound;
        if (!UserCredentials.EmailEditable(user)) return IdentityErrors.EmailManagedByDirectory;
        string? address = null;
        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var checkedAddress = await credentials.CheckEmailAsync(user, request.Email, ct);
            if (!checkedAddress.IsSuccess) return checkedAddress.Error;
            address = checkedAddress.Value;
        }
        // 同一地址不重設：否則本人已驗證的信箱會被降為未驗證。
        if (user.Email is null ? address is null : UserEmails.Same(user.Email, address)) return Result.Success;
        var previous = user.EmailVerifiedAt is null ? null : user.Email;
        user.Email = address;
        user.EmailSource = address is null ? null : EmailSources.Admin;
        user.EmailVerifiedAt = null;
        events.Raise(new UserEmailChanged(user.Id, user.DisplayName, previous, address, actor));
        return Result.Success;
    }, ct);

    /// <summary><c>admin.user_email</c> 的稽核快照：信箱只記遮罩後的值。</summary>
    private async Task<object?> SnapshotAsync(Guid id, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.Email, x.EmailSource, x.EmailVerifiedAt }).SingleOrDefaultAsync(ct);
        return user is null ? null : new { email = user.Email is null ? null : UserEmails.Mask(user.Email), source = user.EmailSource, verified = user.EmailVerifiedAt is not null };
    }
}
