using System.Linq.Expressions;
using AiNexus.Features.Persistence;

namespace AiNexus.Features.Audit;

/// <summary>One classification expression serves SQL filtering and historical record presentation.</summary>
public static class AuditCategories
{
    public static readonly string[] Values = ["authentication", "administration", "access", "activity"];
    private static readonly string[] Reads = ["admin.user_usage_read", "admin.conversations_list", "admin.conversation_read",
        "billing.report.read", "billing.report.export", "dashboard.platform.read", "integration.searched", "integration.record.read"];
    private static readonly Expression<Func<AuditEvent, string>> Category = entry =>
        entry.Action.StartsWith("identity.") ? "authentication" :
        entry.Action.StartsWith("logs.") || Reads.Contains(entry.Action) ? "access" :
        entry.Action.StartsWith("admin.") || entry.Action.StartsWith("resource.acl") ||
        entry.Action == "billing.price.created" || entry.Action == "system.diagnostics.configuration" ? "administration" : "activity";
    private static readonly Func<AuditEvent, string> Classify = Category.Compile();
    public static string For(string action) => Classify(new() { Action = action });
    /// <summary>The caller checks <paramref name="category"/> against <see cref="Values"/> and reports its own error.</summary>
    public static IQueryable<AuditEvent> Filter(IQueryable<AuditEvent> query, string? category)
    {
        if (string.IsNullOrEmpty(category)) return query;
        if (!Values.Contains(category)) throw new ArgumentOutOfRangeException(nameof(category));
        return query.Where(Expression.Lambda<Func<AuditEvent, bool>>(
            Expression.Equal(Category.Body, Expression.Constant(category)), Category.Parameters));
    }
}
