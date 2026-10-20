using AiNexus.Data.App.Mail;

namespace AiNexus.Features.Admin.Mail;

/// <summary>
/// 寫在呼叫模組裡的信件範本。分類、優先序與是否敏感是範本的屬性，不由呼叫端每次決定；內容只能用 <see cref="MailBody"/> 的語意區塊組成，
/// 值一律編碼，範本無法插入原始 HTML。範本在入列時渲染，紀錄保存的就是寄出的內容。
/// </summary>
public abstract class MailTemplate<TModel>
{
    /// <summary>信件分類（≤64，小寫點分或 kebab，例：<c>account.email-change-code</c>），供篩選與冪等鍵前綴。</summary>
    public abstract string Category { get; }

    public virtual MailPriority Priority => MailPriority.Normal;

    /// <summary>
    /// 含驗證碼等秘密的信：內文加密保存、寄出或結束後立即刪除、管理端看不到內文也不能重寄。主旨不加密，不可放秘密。
    /// </summary>
    public virtual bool Sensitive => false;

    public abstract string Subject(TModel model);

    public abstract void Body(TModel model, MailBody body);
}
