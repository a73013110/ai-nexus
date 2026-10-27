using AiNexus.Platform.Auditing;

namespace AiNexus.Features.Admin.Billing;

/// <summary>計費模組的稽核動作（ADR 0023）。名稱是稽核合約，寫入後不可改。</summary>
public static class BillingAudit
{
    public static readonly AuditAction PriceRead = AuditAction.Access("billing.price.read", "查詢模型價格");
    public static readonly AuditAction PriceCreated = AuditAction.Administration("billing.price.created", "新增價格版本");
    public static readonly AuditAction SpendRead = AuditAction.Access("billing.spend.read", "檢視費用報表");
    public static readonly AuditAction SpendExported = AuditAction.Access("billing.spend.exported", "匯出費用報表");
}
