using AiNexus.Features.AccessControl;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using AiNexus.Features.Identity.Authentication;
using AiNexus.Features.Identity.Sessions;
using AiNexus.Features.Identity.Users;

namespace AiNexus.Features.Administration.Users;

public sealed record UserAccountRequest(string DisplayName, bool Enabled, bool AdEnabled, bool LocalEnabled, string? AdAccount, string? LocalAccount, IReadOnlyList<string> RoleIds, string? Password = null);

public sealed record CreatedUserDto(Guid Id);

/// <summary>
/// Login account names are checked first, with their own code; the name, roles and login methods are checked
/// afterwards by <see cref="ManageUserAccounts"/>, in that order.
/// </summary>
internal sealed class UserAccountRequestValidator : RequestValidator<UserAccountRequest>
{
    public override string ProblemCode => "invalid_account";

    public UserAccountRequestValidator()
    {
        RuleFor(x => x.LocalAccount).Must(UserAccounts.IsValid).WithErrorCode("format");
        RuleFor(x => x.AdAccount).Must(UserAccounts.IsValid).WithErrorCode("format");
    }
}

/// <summary>Creates, edits and deletes accounts; <see cref="AdministrativeAudit"/> audits each change.</summary>
internal sealed class ManageUserAccounts(NexusDbContext db, CurrentUser current, AdministrativeAudit audit, Argon2Passwords passwords, IHttpContextAccessor http, TimeProvider clock)
{
    public static void MapCreate(RouteGroupBuilder routes) => routes
        .MapPost("/users", (UserAccountRequest body, ManageUserAccounts handler, CancellationToken ct) => handler.SaveAsync(null, body, ct).ToHttpResultAsync(id => TypedResults.Ok(new CreatedUserDto(id))))
        .WithName("CreateAdminUser");

    public static void MapUpdate(RouteGroupBuilder routes) => routes
        .MapPut("/users/{id:guid}", (Guid id, UserAccountRequest body, ManageUserAccounts handler, CancellationToken ct) => handler.SaveAsync(id, body, ct).ToHttpResultAsync(_ => TypedResults.NoContent()))
        .WithName("SaveAdminUser");

    public static void MapDelete(RouteGroupBuilder routes) => routes
        .MapDelete("/users/{id:guid}", (Guid id, ManageUserAccounts handler, CancellationToken ct) => handler.DeleteAsync(id, ct).ToHttpResultAsync())
        .WithName("DeleteAdminUser");

    public async Task<Result<Guid>> SaveAsync(Guid? id, UserAccountRequest request, CancellationToken ct)
    {
        var signedIn = await current.GetAsync(ct);
        if (!signedIn.IsSuccess) return signedIn.Error;
        var actor = signedIn.Value;
        var local = UserAccounts.Normalize(request.LocalAccount); var ad = UserAccounts.Normalize(request.AdAccount);
        if (string.IsNullOrWhiteSpace(request.DisplayName) || request.DisplayName.Trim().Length > 120 || request.RoleIds is null || request.RoleIds.Count > 100 || request.RoleIds.Distinct().Count() != request.RoleIds.Count)
            return AdministrationErrors.InvalidUser;
        if (!request.AdEnabled && !request.LocalEnabled || request.AdEnabled && ad is null || request.LocalEnabled && local is null)
            return AdministrationErrors.InvalidLoginMethods;
        if (request.Password is { Length: > 0 } candidate && !Argon2Passwords.IsAcceptable(candidate))
            return AdministrationErrors.InvalidPassword;
        var hash = request.Password is { Length: > 0 } password ? await passwords.HashAsync(password, ct) : null;
        var key = id ?? Guid.NewGuid();
        var saved = await audit.MutateAsync("admin.user", key, key.ToString(), async () =>
        {
            var user = id is null ? new NexusUser { Id = key, Sid = "managed:" + key, ProfileManaged = true, LastSeenAt = clock.GetUtcNow() } :
                await db.Users.SingleOrDefaultAsync(x => x.Id == key && x.DeletedAt == null, ct);
            if (user is null) return AdministrationErrors.UserNotFound;
            if (request.LocalEnabled && hash is null && user.PasswordHash is null)
                return AdministrationErrors.LocalPasswordRequired;
            var currentMethod = http.HttpContext?.User.FindFirst(SessionIdentity.Method)?.Value ?? "ad";
            if (key == actor.Id && (!request.Enabled || currentMethod == "local" && !request.LocalEnabled || currentMethod != "local" && !request.AdEnabled))
                return AdministrationErrors.Lockout;
            if (await db.Users.AnyAsync(x => x.Id != key && (local != null && x.LocalAccount == local || ad != null && x.AdAccount == ad), ct))
                return AdministrationErrors.AccountExists;
            // A pre-provisioned AD name must not take over an existing, SID-bound user.
            if (ad is not null)
            {
                var accounts = await db.Users.AsNoTracking().Where(x => x.Id != key && x.AdAccount == null && !x.Sid.StartsWith("managed:")).Select(x => x.Account).ToListAsync(ct);
                if (accounts.Any(x => string.Equals(UserAccounts.AccountName(x), ad, StringComparison.OrdinalIgnoreCase)))
                    return AdministrationErrors.AccountExists;
                if (!user.Sid.StartsWith("managed:", StringComparison.Ordinal) && !string.Equals(UserAccounts.AccountName(user.Account), ad, StringComparison.OrdinalIgnoreCase))
                    return AdministrationErrors.AdBindingImmutable;
            }
            var validRoles = await db.Set<Role>().CountAsync(x => request.RoleIds.Contains(x.Id), ct);
            if (validRoles != request.RoleIds.Count) return AdministrationErrors.InvalidRoles;
            if (id is null) db.Users.Add(user);
            if (user.Enabled != request.Enabled || user.AdEnabled != request.AdEnabled || user.LocalEnabled != request.LocalEnabled || user.AdAccount != ad || user.LocalAccount != local || hash is not null)
                user.SecurityVersion++;
            user.DisplayName = request.DisplayName.Trim(); user.ProfileManaged = true;
            user.Enabled = request.Enabled; user.AdEnabled = request.AdEnabled; user.LocalEnabled = request.LocalEnabled;
            user.AdAccount = ad; user.LocalAccount = local;
            if (user.Sid.StartsWith("managed:", StringComparison.Ordinal)) user.Account = local ?? ad!;
            if (hash is not null) { user.PasswordHash = hash; user.FailedLogins = 0; user.LockedUntil = null; }
            var roles = await db.Set<UserRole>().Where(x => x.UserId == key).ToListAsync(ct);
            db.RemoveRange(roles.Where(x => !request.RoleIds.Contains(x.RoleId)));
            db.AddRange(request.RoleIds.Where(x => roles.All(y => y.RoleId != x)).Select(x => new UserRole { UserId = key, RoleId = x }));
            return Result.Success;
        }, ct);
        return saved.IsSuccess ? key : saved.Error;
    }

    public Task<Result> DeleteAsync(Guid id, CancellationToken ct) => audit.MutateAsync("admin.user_delete", id, id.ToString(), async () =>
        {
            var user = await db.Users.SingleOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);
            if (user is null) return AdministrationErrors.UserNotFound;
            user.DeletedAt = clock.GetUtcNow(); user.Enabled = false; user.AdEnabled = false; user.LocalEnabled = false;
            user.PasswordHash = null; user.SecurityVersion++;
            // Keep SID, names, resources and historical relationships to prevent takeover or cascading loss.
            return Result.Success;
        }, ct);
}
