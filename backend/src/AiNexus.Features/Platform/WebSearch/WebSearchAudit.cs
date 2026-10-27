using AiNexus.Platform.Auditing;

namespace AiNexus.Features.Platform.WebSearch;

/// <summary>網路搜尋的稽核動作（生成過程中由背景工作記錄）（ADR 0023）。名稱是稽核合約，寫入後不可改。</summary>
public static class WebSearchAudit
{
    public static readonly AuditAction Searched = AuditAction.Access("web-search.query.searched", "執行網路搜尋");
}
