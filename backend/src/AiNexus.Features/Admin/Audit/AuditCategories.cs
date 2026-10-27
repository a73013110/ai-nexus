using AiNexus.Platform.Auditing;

namespace AiNexus.Features.Admin.Audit;

public sealed record AuditCategoryDto(string Value, string Label);

/// <summary>稽核分類（ADR 0023）。分類在寫入時由動作決定並存在 <c>Category</c> 欄位，篩選直接比對欄位。</summary>
public static class AuditCategories
{
    public static readonly IReadOnlyList<AuditCategoryDto> All =
    [
        new(AuditAction.Value(AuditCategory.Authentication), "登入與身分"),
        new(AuditAction.Value(AuditCategory.Administration), "管理異動"),
        new(AuditAction.Value(AuditCategory.Access), "查閱與匯出"),
        new(AuditAction.Value(AuditCategory.Activity), "功能操作"),
    ];

    public static readonly string[] Values = [.. All.Select(x => x.Value)];

    public static string Label(string category) => All.FirstOrDefault(x => x.Value == category)?.Label ?? category;
}
