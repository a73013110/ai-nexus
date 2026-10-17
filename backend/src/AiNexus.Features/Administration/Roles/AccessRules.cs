using System.Text.RegularExpressions;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Administration.Roles;

/// <summary>
/// 角色、群組與功能編輯的格式規則。在 <see cref="AdministrativeAudit.MutateAsync(string, Guid?, string, Func{Task{Result}}, CancellationToken)"/> 內、管理者檢查之後執行，
/// 所以被拒絕的編輯仍會留下錯誤代碼的稽核；因此這些規則刻意不做成 request validator。
/// </summary>
internal static class AccessRules
{
    public static Error? Key(string id) => Regex.IsMatch(id, "^[a-z][a-z0-9_-]{0,63}$", RegexOptions.CultureInvariant) ? null : AdministrationErrors.InvalidAccessId;

    public static Error? Keys(IReadOnlyList<string> ids)
    {
        if (ids.Count > 50 || ids.Distinct().Count() != ids.Count) return AdministrationErrors.InvalidAccessIds;
        foreach (var id in ids) if (Key(id) is { } invalid) return invalid;
        return null;
    }

    public static Error? Name(string name)
        => string.IsNullOrWhiteSpace(name) || name.Trim().Length > 120 || name.Any(char.IsControl)
            ? AdministrationErrors.InvalidAccessName
            : null;

    public static async Task<Error?> ExistingAsync(IQueryable<string> query, IReadOnlyList<string> ids, CancellationToken ct)
        => await query.CountAsync(x => ids.Contains(x), ct) != ids.Count ? AdministrationErrors.UnknownAccessId : null;
}
