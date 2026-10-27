using System.Text.RegularExpressions;

namespace AiNexus.Platform.Auditing;

/// <summary>稽核分類，存在 <c>audit.AuditEvents.Category</c>；值由動作宣告決定，不從名稱推測（ADR 0023）。</summary>
public enum AuditCategory
{
    /// <summary>登入、登出、測試身分與帳號安全。</summary>
    Authentication,
    /// <summary>平台設定、授權與主資料的異動。</summary>
    Administration,
    /// <summary>查詢、檢視、搜尋、匯出與下載。</summary>
    Access,
    /// <summary>使用者在功能裡的其他寫入。</summary>
    Activity,
}

/// <summary>
/// 一個稽核動作，名稱是 <c>&lt;模組&gt;.&lt;對象&gt;.&lt;動詞&gt;</c>（例如 <c>attendance.record.read</c>）。
/// 名稱是稽核合約：寫進資料庫後就是歷史，不跟著類別或端點改名。各模組在根目錄的 <c>&lt;Module&gt;Audit</c> 宣告，
/// 稽核頁的動作清單、標籤與分類都由這裡的宣告取得（ADR 0023）。
/// </summary>
public sealed partial record AuditAction
{
    public const int NameMaxLength = 64;

    /// <summary>屬於「查閱與匯出」的動詞；反過來，其他分類不能用這些動詞。</summary>
    public static readonly IReadOnlySet<string> AccessVerbs = new HashSet<string>(StringComparer.Ordinal) { "read", "searched", "exported", "downloaded" };

    /// <summary>
    /// 允許的寫入動詞。動詞固定，稽核頁才能一致地說「做了什麼」；真的需要新動詞時加在這裡並說明語意。
    /// </summary>
    public static readonly IReadOnlySet<string> ChangeVerbs = new HashSet<string>(StringComparer.Ordinal)
    {
        "created", "updated", "deleted", "restored", "renamed", "moved", "reordered", "duplicated", "imported", "synced", "uploaded",
        "assigned", "unassigned", "granted", "shared", "revoked", "bound", "unbound", "enabled", "disabled", "activated", "rebuilt", "cleared",
        "queued", "cancelled", "retried", "recovered", "finished", "sent", "reviewed", "commented", "connected", "disconnected", "pinned", "unpinned",
        "dismissed", "selected", "requested", "confirmed", "verified", "reported", "diagnosed", "transformed", "retained", "signed_in", "signed_out", "started", "ended",
    };

    private AuditAction(string name, AuditCategory category, string label)
    {
        if (name.Length > NameMaxLength || !NamePattern().IsMatch(name)) throw new ArgumentException($"稽核動作名稱必須是 <模組>.<對象>.<動詞>：{name}", nameof(name));
        var verb = name[(name.LastIndexOf('.') + 1)..];
        if (category == AuditCategory.Access != AccessVerbs.Contains(verb) || category != AuditCategory.Access && !ChangeVerbs.Contains(verb))
            throw new ArgumentException($"稽核動作 {name} 的動詞 {verb} 與分類 {category} 不符。", nameof(name));
        if (string.IsNullOrWhiteSpace(label)) throw new ArgumentException($"稽核動作 {name} 缺少標籤。", nameof(label));
        (Name, Category, Label) = (name, category, label);
    }

    public string Name { get; }
    public AuditCategory Category { get; }
    /// <summary>稽核頁顯示的中文說明，例如「查詢出勤紀錄」。</summary>
    public string Label { get; }
    /// <summary>名稱的第一段：功能目錄的模組 ID，不在目錄裡的平台模組用自己的 kebab-case 名稱。</summary>
    public string Module => Name[..Name.IndexOf('.', StringComparison.Ordinal)];

    public static AuditAction Access(string name, string label) => new(name, AuditCategory.Access, label);
    public static AuditAction Activity(string name, string label) => new(name, AuditCategory.Activity, label);
    public static AuditAction Administration(string name, string label) => new(name, AuditCategory.Administration, label);
    public static AuditAction Authentication(string name, string label) => new(name, AuditCategory.Authentication, label);

    /// <summary>資料庫存的分類值（小寫）。</summary>
    public static string Value(AuditCategory category) => category.ToString().ToLowerInvariant();

    public override string ToString() => Name;

    [GeneratedRegex("^[a-z][a-z-]*\\.[a-z][a-z_]*\\.[a-z][a-z_]*$")]
    private static partial Regex NamePattern();
}
