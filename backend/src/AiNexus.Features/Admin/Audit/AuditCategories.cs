using AiNexus.Data.App.Audit;
using System.Linq.Expressions;

namespace AiNexus.Features.Admin.Audit;

public sealed record AuditCategoryDto(string Value, string Label);

/// <summary>同一個分類運算式同時用於 SQL 篩選（轉成 CASE WHEN）與輸出時的分類，兩邊不會不一致。</summary>
public static class AuditCategories
{
    public static readonly IReadOnlyList<AuditCategoryDto> All = [new("authentication", "登入與身分"), new("administration", "管理異動"), new("access", "查閱與匯出"), new("activity", "功能操作")];
    public static readonly string[] Values = [.. All.Select(x => x.Value)];
    private static readonly string[] Reads = ["admin.user_usage_read", "admin.conversations_list", "admin.conversation_read",
        "billing.report.read", "billing.report.export", "dashboard.platform.read", "integration.searched", "integration.record.read", ExportActivityAudit.Action];
    private static readonly string[] Administrative = ["billing.price.created", "system.diagnostics.configuration"];
    private static readonly Expression<Func<AuditEvent, string>> Category = entry =>
        entry.Action.StartsWith("identity.") ? "authentication" :
        entry.Action.StartsWith("logs.") || Reads.Contains(entry.Action) ? "access" :
        entry.Action.StartsWith("admin.") || entry.Action.StartsWith("resource.acl") || Administrative.Contains(entry.Action) ? "administration" : "activity";
    private static readonly Func<AuditEvent, string> Classify = Category.Compile();
    public static string For(string action) => Classify(new() { Action = action });
    public static string Label(string category) => All.FirstOrDefault(x => x.Value == category)?.Label ?? category;
    /// <summary>
    /// 某個動作篩選（完整動作或前綴）可能查到的紀錄分類，自身分類排第一；稽核頁依此決定動作屬於哪些分類。
    /// 例如 <c>admin.</c> 屬於「管理異動」，但也涵蓋 <c>admin.conversation_read</c> 這類「查閱與匯出」的動作。
    /// </summary>
    public static IReadOnlyList<string> Covering(string action) =>
        [.. Reads.Concat(Administrative).Where(x => x.StartsWith(action, StringComparison.Ordinal)).Prepend(action).Select(For).Distinct()];
    /// <summary>呼叫端應先以 <see cref="Values"/> 驗證並回報錯誤；走到這裡仍遇到未知分類，代表程式有錯，所以直接 throw。</summary>
    public static IQueryable<AuditEvent> Filter(IQueryable<AuditEvent> query, string? category)
    {
        if (string.IsNullOrEmpty(category)) return query;
        if (!Values.Contains(category)) throw new ArgumentOutOfRangeException(nameof(category));
        return query.Where(Expression.Lambda<Func<AuditEvent, bool>>(
            Expression.Equal(Category.Body, Expression.Constant(category)), Category.Parameters));
    }
}
