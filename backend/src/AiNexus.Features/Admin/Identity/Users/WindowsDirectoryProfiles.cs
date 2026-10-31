using System.DirectoryServices.AccountManagement;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace AiNexus.Features.Admin.Identity.Users;

/// <summary>以執行身分（IIS 應用程式集區）查網域的 <see cref="IDirectoryProfileLookup"/>；不在 Windows 上時一律回 null。</summary>
public sealed partial class WindowsDirectoryProfiles(ILogger<WindowsDirectoryProfiles> logger) : IDirectoryProfileLookup
{
    public DirectoryProfile? Find(string sid, string account) => OperatingSystem.IsWindows() ? FindInDomain(sid, account) : null;

    [SupportedOSPlatform("windows")]
    private DirectoryProfile? FindInDomain(string sid, string account)
    {
        try
        {
            using var context = new PrincipalContext(ContextType.Domain);
            using var user = UserPrincipal.FindByIdentity(context, IdentityType.Sid, sid);
            if (user is null) return null;
            return new(user.DisplayName is { Length: > 0 } name ? name : UserAccounts.AccountName(account), user.EmailAddress);
        }
        // 網域控制站連不上、權限不足或 COM 層錯誤都屬於目錄暫時不可用；其他例外是程式錯誤，照常拋出。
        catch (Exception error) when (error is PrincipalException or ExternalException)
        {
            LogLookupFailed(logger, error.GetType().Name);
            return null;
        }
    }

    [LoggerMessage(EventId = 7003, EventName = "identity.directory_profile_unavailable", Level = LogLevel.Warning,
        Message = "Directory profile lookup failed ({ErrorType}); the user's name and email are left unchanged.")]
    private static partial void LogLookupFailed(ILogger logger, string errorType);
}
