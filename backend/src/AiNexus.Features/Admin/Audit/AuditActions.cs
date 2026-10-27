using System.Reflection;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Auditing;

namespace AiNexus.Features.Admin.Audit;

/// <summary>一個稽核動作：名稱、顯示名稱、分類與所屬模組（名稱第一段）。</summary>
public sealed record AuditActionDto(string Value, string Label, string Category, string Module);

/// <summary>稽核動作所屬的模組；動作篩選可只選模組（前綴 <c>&lt;module&gt;.</c>）。</summary>
public sealed record AuditModuleDto(string Value, string Label);

/// <summary>
/// 所有宣告的稽核動作：各模組 <c>&lt;Module&gt;Audit</c> 類別裡型別為 <see cref="AuditAction"/> 的公開靜態欄位。
/// 稽核頁的動作選單、名稱與分類都來自這裡，前端不寫死；新增動作只要在模組裡宣告。依功能目錄的模組順序、再依宣告順序排列。
/// </summary>
public static class AuditActions
{
    // 不在功能目錄裡的平台模組（沒有自己的頁面）。
    private static readonly Dictionary<string, string> PlatformModules = new(StringComparer.Ordinal)
    {
        ["account"] = "帳號",
        ["remote-files"] = "遠端檔案",
        ["web-search"] = "網路搜尋",
    };

    /// <summary>宣告的全部動作；同一個動作被多個類別引用時只列一次。</summary>
    public static IReadOnlyList<AuditAction> Declared { get; } = [.. typeof(AuditActions).Assembly.GetTypes()
        .Where(x => x is { IsAbstract: true, IsSealed: true })
        .OrderBy(x => x.FullName, StringComparer.Ordinal)
        .SelectMany(x => x.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
        .Where(x => x.FieldType == typeof(AuditAction))
        .Select(x => (AuditAction)x.GetValue(null)!)
        .DistinctBy(x => x.Name)];

    public static IReadOnlyList<AuditModuleDto> Modules { get; } = [.. Declared.Select(x => x.Module).Distinct().OrderBy(Order).Select(x => new AuditModuleDto(x, ModuleLabel(x)))];

    public static IReadOnlyList<AuditActionDto> All { get; } = [.. Declared
        .Select((action, index) => (action, index)).OrderBy(x => Order(x.action.Module)).ThenBy(x => x.index)
        .Select(x => new AuditActionDto(x.action.Name, x.action.Label, AuditAction.Value(x.action.Category), x.action.Module))];

    private static int Order(string module)
    {
        var index = FeatureCatalog.Modules.Select(x => x.Id).ToList().IndexOf(module);
        return index >= 0 ? index : FeatureCatalog.Modules.Count + PlatformModules.Keys.ToList().IndexOf(module);
    }

    private static string ModuleLabel(string module)
        => FeatureCatalog.Modules.FirstOrDefault(x => x.Id == module)?.Name ?? PlatformModules.GetValueOrDefault(module, module);
}
