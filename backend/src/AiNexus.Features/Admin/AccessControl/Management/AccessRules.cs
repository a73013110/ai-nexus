using System.Text.RegularExpressions;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.AccessControl.Management;

/// <summary>
/// 角色、群組與功能編輯的格式規則。在 <see cref="AdministrativeAudit.MutateAsync(Guid, string, string, Guid?, string, Func{CancellationToken, Task{object}}, Func{Task{Result}}, CancellationToken)"/> 內、管理者檢查之後執行，
/// 所以被拒絕的編輯仍會留下錯誤代碼的稽核；因此這些規則刻意不做成 request validator。Identity 與 Operations 的管理端點也共用。
/// </summary>
public static class AccessRules
{
    public static Error? Key(string id) => Regex.IsMatch(id, "^[a-z][a-z0-9_-]{0,63}$", RegexOptions.CultureInvariant) ? null : AccessControlErrors.InvalidAccessId;

    public static Error? Keys(IReadOnlyList<string> ids) => Keys(ids, Key);

    /// <summary>
    /// 功能 id 有階層（<c>diagnostics.logs.export</c>），格式比角色、群組 id 多允許點號；實際是否存在由
    /// <see cref="ExistingAsync"/> 比對資料庫，目錄不認得的舊列仍可授予或撤銷。
    /// </summary>
    public static Error? FeatureKeys(IReadOnlyList<string> ids) => Keys(ids, FeatureKey);

    private static Error? FeatureKey(string id)
        => Regex.IsMatch(id, "^[a-z][a-z0-9_.-]{0,63}$", RegexOptions.CultureInvariant) ? null : AccessControlErrors.InvalidAccessId;

    private static Error? Keys(IReadOnlyList<string> ids, Func<string, Error?> key)
    {
        if (ids.Count > 50 || ids.Distinct().Count() != ids.Count) return AccessControlErrors.InvalidAccessIds;
        foreach (var id in ids) if (key(id) is { } invalid) return invalid;
        return null;
    }

    public static Error? Name(string name)
        => string.IsNullOrWhiteSpace(name) || name.Trim().Length > 120 || name.Any(char.IsControl)
            ? AccessControlErrors.InvalidAccessName
            : null;

    /// <summary>
    /// 群組授權的資料範圍（ADR 0017）：目錄有宣告範圍的功能必須選一個它支援的範圍，其他功能（含目錄不認得的舊列）不可帶範圍；
    /// 範圍只能給這次授予的功能。
    /// </summary>
    public static Error? Scopes(IReadOnlyList<string> featureIds, IReadOnlyDictionary<string, string>? scopes)
    {
        scopes ??= new Dictionary<string, string>();
        if (scopes.Keys.Any(x => !featureIds.Contains(x))) return AccessControlErrors.InvalidDataScope;
        foreach (var id in featureIds)
        {
            var supported = FeatureCatalog.Find(id)?.Scopes ?? [];
            var scope = scopes.GetValueOrDefault(id);
            if (supported.Count == 0 ? scope is not null : scope is null || !supported.Contains(scope)) return AccessControlErrors.InvalidDataScope;
        }
        return null;
    }

    public static async Task<Error?> ExistingAsync(IQueryable<string> query, IReadOnlyList<string> ids, CancellationToken ct)
        => await query.CountAsync(x => ids.Contains(x), ct) != ids.Count ? AccessControlErrors.UnknownAccessId : null;
}
