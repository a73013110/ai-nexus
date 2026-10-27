using AiNexus.Platform.Auditing;

namespace AiNexus.Features.Platform.Account;

/// <summary>帳號（個人設定與帳號安全）的稽核動作（ADR 0023）。名稱是稽核合約，寫入後不可改。</summary>
public static class AccountAudit
{
    public static readonly AuditAction SettingsUpdated = AuditAction.Activity("account.settings.updated", "修改個人設定");
    public static readonly AuditAction Reauthenticated = AuditAction.Authentication("account.session.verified", "重新驗證身分");
    public static readonly AuditAction VerificationCodeSent = AuditAction.Authentication("account.verification_code.sent", "寄送驗證碼");
    public static readonly AuditAction EmailRequested = AuditAction.Authentication("account.email.requested", "申請變更信箱");
    public static readonly AuditAction EmailConfirmed = AuditAction.Authentication("account.email.confirmed", "確認變更信箱");
    public static readonly AuditAction LocalLoginEnabled = AuditAction.Authentication("account.local_login.enabled", "啟用本地登入");
    public static readonly AuditAction LocalAccountRenamed = AuditAction.Authentication("account.local_account.renamed", "變更本地登入帳號");
    public static readonly AuditAction PasswordUpdated = AuditAction.Authentication("account.password.updated", "變更本地登入密碼");
    public static readonly AuditAction LocalLoginDisabled = AuditAction.Authentication("account.local_login.disabled", "停用本地登入");
}
