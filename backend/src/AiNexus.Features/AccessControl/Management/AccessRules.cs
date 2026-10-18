using System.Text.RegularExpressions;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.AccessControl.Management;

/// <summary>
/// 角色、群組與功能編輯的格式規則。在 <see cref="AdministrativeAudit.MutateAsync(Guid, string, Guid?, string, Func{CancellationToken, Task{object}}, Func{Task{Result}}, CancellationToken)"/> 內、管理者檢查之後執行，
/// 所以被拒絕的編輯仍會留下錯誤代碼的稽核；因此這些規則刻意不做成 request validator。Identity 與 Operations 的管理端點也共用。
/// </summary>
public static class AccessRules
{
    public static Error? Key(string id) => Regex.IsMatch(id, "^[a-z][a-z0-9_-]{0,63}$", RegexOptions.CultureInvariant) ? null : AccessControlErrors.InvalidAccessId;

    public static Error? Keys(IReadOnlyList<string> ids)
    {
        if (ids.Count > 50 || ids.Distinct().Count() != ids.Count) return AccessControlErrors.InvalidAccessIds;
        foreach (var id in ids) if (Key(id) is { } invalid) return invalid;
        return null;
    }

    public static Error? Name(string name)
        => string.IsNullOrWhiteSpace(name) || name.Trim().Length > 120 || name.Any(char.IsControl)
            ? AccessControlErrors.InvalidAccessName
            : null;

    public static async Task<Error?> ExistingAsync(IQueryable<string> query, IReadOnlyList<string> ids, CancellationToken ct)
        => await query.CountAsync(x => ids.Contains(x), ct) != ids.Count ? AccessControlErrors.UnknownAccessId : null;
}
