using System.Linq.Expressions;
using AiNexus.Features.Persistence;

namespace AiNexus.Features.Audit;

public sealed record AuditCategoryDto(string Value, string Label);

/// <summary>One classification expression serves SQL filtering and historical record presentation.</summary>
public static class AuditCategories
{
    public static readonly string[] Values = ["authentication", "administration", "access", "activity"];
    public static readonly IReadOnlyList<AuditCategoryDto> All = [new("authentication", "登入與身分"), new("administration", "管理異動"), new("access", "查閱與匯出"), new("activity", "功能操作")];
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
    /// The categories of the records an action filter (a stored action or a prefix) can match; the prefix's own category
    /// comes first. <c>admin.</c> covers administration and the administrative reads.
    /// </summary>
    public static IReadOnlyList<string> Covering(string action) =>
        [.. Reads.Concat(Administrative).Where(x => x.StartsWith(action, StringComparison.Ordinal)).Prepend(action).Select(For).Distinct()];
    /// <summary>The caller checks <paramref name="category"/> against <see cref="Values"/> and reports its own error.</summary>
    public static IQueryable<AuditEvent> Filter(IQueryable<AuditEvent> query, string? category)
    {
        if (string.IsNullOrEmpty(category)) return query;
        if (!Values.Contains(category)) throw new ArgumentOutOfRangeException(nameof(category));
        return query.Where(Expression.Lambda<Func<AuditEvent, bool>>(
            Expression.Equal(Category.Body, Expression.Constant(category)), Category.Parameters));
    }
}
