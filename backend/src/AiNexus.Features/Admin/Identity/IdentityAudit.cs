using AiNexus.Platform.Auditing;

namespace AiNexus.Features.Admin.Identity;

/// <summary>身分模組的稽核動作：登入、測試身分與使用者管理（ADR 0023）。名稱是稽核合約，寫入後不可改。</summary>
public static class IdentityAudit
{
    public static readonly AuditAction SignedIn = AuditAction.Authentication("identity.session.signed_in", "登入");
    public static readonly AuditAction SignedOut = AuditAction.Authentication("identity.session.signed_out", "登出");
    public static readonly AuditAction IdentitySelected = AuditAction.Authentication("identity.session.selected", "切換身分");
    public static readonly AuditAction TestIdentityStarted = AuditAction.Authentication("identity.test_identity.started", "開始測試身分");
    public static readonly AuditAction TestIdentityEnded = AuditAction.Authentication("identity.test_identity.ended", "結束測試身分");
    public static readonly AuditAction AccountCreated = AuditAction.Authentication("identity.account.created", "首次登入建立帳號");
    public static readonly AuditAction AdministratorGranted = AuditAction.Administration("identity.administrator.granted", "初始化管理員");

    public static readonly AuditAction UserRead = AuditAction.Access("identity.user.read", "查詢使用者");
    public static readonly AuditAction UserCreated = AuditAction.Administration("identity.user.created", "建立使用者");
    public static readonly AuditAction UserUpdated = AuditAction.Administration("identity.user.updated", "修改使用者");
    public static readonly AuditAction UserDeleted = AuditAction.Administration("identity.user.deleted", "移除使用者");
    public static readonly AuditAction UserMoved = AuditAction.Administration("identity.user.moved", "調整使用者排序");
    public static readonly AuditAction UserReordered = AuditAction.Administration("identity.user.reordered", "重設使用者排序");
    public static readonly AuditAction UserAccessRead = AuditAction.Access("identity.user_access.read", "檢視使用者權限");
    public static readonly AuditAction UserRoleUpdated = AuditAction.Administration("identity.user_role.updated", "調整使用者角色");
    public static readonly AuditAction UserEmailUpdated = AuditAction.Administration("identity.user_email.updated", "設定使用者信箱");
    public static readonly AuditAction DirectoryBound = AuditAction.Administration("identity.user_directory.bound", "改綁使用者 AD 帳號");
    public static readonly AuditAction DirectoryUnbound = AuditAction.Administration("identity.user_directory.unbound", "解除使用者 AD 綁定");
    public static readonly AuditAction AccountReviewRead = AuditAction.Access("identity.account_review.read", "查詢帳號檢查");

    public static readonly AuditAction DirectoryRead = AuditAction.Access("identity.directory.read", "瀏覽 AD 目錄");
    public static readonly AuditAction DirectoryPersonRead = AuditAction.Access("identity.directory_person.read", "查詢 AD 人員");
    public static readonly AuditAction DirectoryImportRead = AuditAction.Access("identity.directory_import.read", "預覽 AD 匯入或更新");
    public static readonly AuditAction DirectoryBatchImported = AuditAction.Administration("identity.directory_batch.imported", "從 AD 匯入使用者（批次）");
    public static readonly AuditAction DirectoryBatchSynced = AuditAction.Administration("identity.directory_batch.synced", "以 AD 資料更新使用者（批次）");
    public static readonly AuditAction UserImported = AuditAction.Administration("identity.user.imported", "從 AD 匯入使用者");
    public static readonly AuditAction UserSynced = AuditAction.Administration("identity.user.synced", "以 AD 資料更新使用者");
}
