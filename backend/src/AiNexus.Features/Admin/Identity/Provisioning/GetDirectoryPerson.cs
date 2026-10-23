using AiNexus.Platform.Errors;

namespace AiNexus.Features.Admin.Identity.Provisioning;

/// <summary>AD 屬性面板的一位人員；欄位說明見 <see cref="DirectoryPersonDetail"/>。</summary>
public sealed record DirectoryPersonDetailDto(
    string Sid, string? ObjectGuid, string Dn, string Path, string Account, string DisplayName, string? GivenName, string? Surname, string? Upn, string? Email,
    string? EmployeeId, string? Description, string? Company, string? Department, string? Title, string? Manager, string? Office, string? Phone, string? Mobile,
    int AccountControl, DateTimeOffset? AccountExpires, DateTimeOffset? PasswordLastSet, bool PasswordMustChange, DateTimeOffset? PasswordExpires,
    DateTimeOffset? LockoutTime, int? BadPasswordCount, DateTimeOffset? BadPasswordTime, DateTimeOffset? LastLogon, DateTimeOffset? LastLogonTimestamp,
    int? LogonCount, DateTimeOffset? WhenCreated, DateTimeOffset? WhenChanged, IReadOnlyList<string> Groups, bool MoreGroups);

/// <summary>
/// 匯入對話框檢視一位人員的完整 AD 屬性（<c>identity.users.import</c>）。清單只帶匯入需要的欄位，其他屬性在點選人員時才讀，
/// 一次最多上千人的清單不必為很少被看的屬性多付流量與查詢成本。只讀、不寫稽核；限流與目錄樹分開計算。
/// </summary>
internal sealed class GetDirectoryPerson(IAdDirectory directory)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("/directory-people/{sid}", (string sid, GetDirectoryPerson handler, CancellationToken ct) => handler.HandleAsync(sid, ct).ToHttpResultAsync())
        .RequireRateLimiting(IdentityModule.DirectoryPersonRateLimit)
        .WithName("GetDirectoryPerson");

    public async Task<Result<DirectoryPersonDetailDto>> HandleAsync(string sid, CancellationToken ct)
    {
        if (!DirectoryProvisioning.IsSid(sid) || await directory.DetailAsync(sid, ct) is not { } x) return IdentityErrors.DirectoryPersonNotFound;
        return new DirectoryPersonDetailDto(x.Sid, x.ObjectGuid, x.Dn, x.Path, x.AccountName, x.DisplayName, x.GivenName, x.Surname, x.Upn, x.Email, x.EmployeeId,
            x.Description, x.Company, x.Department, x.Title, x.Manager, x.Office, x.Phone, x.Mobile, x.AccountControl, x.AccountExpires, x.PasswordLastSet,
            x.PasswordMustChange, x.PasswordExpires, x.LockoutTime, x.BadPasswordCount, x.BadPasswordTime, x.LastLogon, x.LastLogonTimestamp, x.LogonCount,
            x.WhenCreated, x.WhenChanged, x.Groups, x.MoreGroups);
    }
}
