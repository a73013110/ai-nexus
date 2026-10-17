using System.Security.Claims;
using System.Text.Json;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Security;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using AiNexus.Features.Identity.Authentication;

namespace AiNexus.Features.Identity.Sessions;

public sealed record TestIdentityDto(Guid AdministratorId, string AdministratorName, Guid UserId, DateTimeOffset ExpiresAt);

/// <summary>登入帳號可切換成的身分；目前 <c>Kind</c> 只有 <c>personal</c>。</summary>
public sealed record SignInIdentityDto(string Id, string Kind, string Name, string? Detail);

public sealed record AuthSessionDto(string Mode, bool Authenticated, bool Configured, string? Account, string? DisplayName, string CsrfToken,
    IReadOnlyList<string>? Methods = null, string? Method = null, Guid? UserId = null, TestIdentityDto? Testing = null,
    IReadOnlyList<SignInIdentityDto>? Identities = null, string? Identity = null);

/// <summary>所有 auth 端點共用的 session 描述，以及測試身分相關的共用計算。</summary>
internal static class AuthSession
{
    public const string PersonalIdentity = "personal";

    /// <summary>
    /// 登入後可選擇的身分清單。目前每個帳號只有個人身分，所以登入頁會略過選擇。未來若加入其他來源（組織、委派），
    /// 在這裡新增選項時，也必須同時決定每個身分帶有哪些權限，並稽核使用者的選擇。
    /// </summary>
    public static IReadOnlyList<SignInIdentityDto> Identities(ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true
        ? [new(PersonalIdentity, PersonalIdentity, user.FindFirstValue("display_name") ?? user.Identity.Name ?? "", user.Identity.Name)]
        : [];

    public static AuthSessionDto Describe(HttpContext http, AdAuthenticationOptions options, IAntiforgery csrf)
    {
        WebSecurity.NoStore(http.Response);
        TestIdentityDto? testing = null;
        if (Guid.TryParse(http.User.FindFirstValue(SessionIdentity.ActorId), out var actorId)
            && Guid.TryParse(http.User.FindFirstValue(SessionIdentity.UserId), out var targetId)
            && long.TryParse(http.User.FindFirstValue(SessionIdentity.TestExpires), out var seconds))
            testing = new(actorId, http.User.FindFirstValue("nexus_actor_name") ?? "管理者", targetId, DateTimeOffset.FromUnixTimeSeconds(seconds));
        var methods = new List<string> { "local" };
        if (options.Mode == "Windows") methods.Insert(0, "windows"); else if (options.Configured) methods.Insert(0, "ad");
        var identities = Identities(http.User);
        var chosen = http.User.FindFirstValue(SessionIdentity.ActiveIdentity);
        return new(options.Mode, http.User.Identity?.IsAuthenticated == true, true, http.User.Identity?.Name, http.User.FindFirstValue("display_name"), csrf.GetAndStoreTokens(http).RequestToken!,
            methods, http.User.Identity?.IsAuthenticated == true ? http.User.FindFirstValue(SessionIdentity.Method) ?? (options.Mode == "Windows" ? "windows" : "ad") : null,
            Guid.TryParse(http.User.FindFirstValue(SessionIdentity.UserId), out var id) ? id : null, testing,
            identities, identities.FirstOrDefault(x => x.Id == chosen)?.Id ?? (identities.Count > 0 ? identities[0].Id : null));
    }

    /// <summary>記錄 <paramref name="previous"/> 攜帶的測試身分已結束；若已記錄過就不重複寫入。</summary>
    public static async Task EndExistingTestAsync(ClaimsPrincipal previous, NexusDbContext db, string reason, CancellationToken ct)
    {
        if (!Guid.TryParse(previous.FindFirstValue(SessionIdentity.ActorId), out var actorId) ||
            !Guid.TryParse(previous.FindFirstValue(SessionIdentity.TestId), out var testId) ||
            !Guid.TryParse(previous.FindFirstValue(SessionIdentity.UserId), out var targetId)) return;
        if (await db.AuditEvents.AnyAsync(x => x.OwnerId == actorId && x.ResourceId == testId && x.Action == "identity.test_end", ct)) return;
        db.AuditEvents.Add(new()
        {
            OwnerId = actorId,
            ActorId = actorId,
            ResourceId = testId,
            Action = "identity.test_end",
            Result = "revoked",
            DetailsJson = JsonSerializer.Serialize(new { testId, userId = targetId, reason })
        });
        await db.SaveChangesAsync(ct);
    }
}
