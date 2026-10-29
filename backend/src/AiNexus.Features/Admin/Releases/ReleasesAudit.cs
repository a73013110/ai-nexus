using AiNexus.Platform.Auditing;

namespace AiNexus.Features.Admin.Releases;

/// <summary>版本模組的稽核動作（ADR 0023）。名稱是稽核合約，寫入後不可改。</summary>
public static class ReleasesAudit
{
    // procedure 本文可能含業務規則與帳號資訊，讀取與帶走都留紀錄；版本與部署歷史不含內容，不稽核。
    public static readonly AuditAction SchemaChangesRead = AuditAction.Access("releases.schema_changes.read", "檢視結構異動");
    public static readonly AuditAction SchemaObjectRead = AuditAction.Access("releases.schema_object.read", "檢視結構差異");
    public static readonly AuditAction SchemaChangesExported = AuditAction.Access("releases.schema_changes.exported", "下載異動物件的定義");
}
