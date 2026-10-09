using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Identity.Users;

[Comment("使用者身分、AD SID 綁定、可用登入方式與工作階段撤銷版本；不保存 AD 密碼。")]
public sealed class NexusUser
{
    [Comment("資料的主鍵識別碼。")]
    public Guid Id { get; set; } = Guid.NewGuid();
    [Comment("AD 的不可變 SID；尚未綁定 AD 的手動帳號使用 managed: 識別碼。")]
    public string Sid { get; set; } = "";
    [Comment("登入身分顯示帳號；AD 連結後保存目錄提供的帳號。")]
    public string Account { get; set; } = "";
    [Comment("使用者的介面顯示名稱。")]
    public string DisplayName { get; set; } = "";
    [Comment("是否啟用；停用不刪除歷史資料。")]
    public bool Enabled { get; set; } = true;
    [Comment("管理者設定的個人容量上限 bytes；優先於群組，空值使用群組或預設 5 GB。")]
    public long? AttachmentLimitBytes { get; set; }
    [Comment("登入身分刪除時間；保留關聯與歷史資料。")]
    public DateTimeOffset? DeletedAt { get; set; }
    [Comment("是否允許使用 AD／Windows 整合驗證登入。")]
    public bool AdEnabled { get; set; } = true;
    [Comment("是否允許本地密碼登入；與 AD 驗證獨立。")]
    public bool LocalEnabled { get; set; }
    [Comment("預先配置的 AD 帳號正規化值；唯一、不含網域，驗證成功後以 SID 固定綁定。")]
    public string? AdAccount { get; set; }
    [Comment("本地登入帳號正規化值；唯一且不區分大小寫。")]
    public string? LocalAccount { get; set; }
    [Comment("本地密碼的 Argon2id PHC 雜湊，含版本、成本、隨機 salt 與衍生值；不可還原。")]
    public string? PasswordHash { get; set; }
    [Comment("登入政策或密碼變更時遞增，立即撤銷舊工作階段。")]
    public int SecurityVersion { get; set; }
    [Comment("姓名是否由管理者維護；開啟後 AD 目錄不覆寫姓名。")]
    public bool ProfileManaged { get; set; }
    [Comment("本地登入連續失敗次數，用於暫時鎖定。")]
    public int FailedLogins { get; set; }
    [Comment("本地帳號暫時鎖定的到期時間；空值表示未鎖定。")]
    public DateTimeOffset? LockedUntil { get; set; }
    [Comment("使用者最近登入／活動時間，採 UTC offset。")]
    public DateTimeOffset LastSeenAt { get; set; }
    public UserPreferences Preferences { get; set; } = new();
}

internal sealed class NexusUserConfiguration : IEntityTypeConfiguration<NexusUser>
{
    public void Configure(EntityTypeBuilder<NexusUser> user)
    {
        user.ToTable("Users", "identity", table => table.HasCheckConstraint("CK_Users_AttachmentLimitBytes", "[AttachmentLimitBytes] IS NULL OR [AttachmentLimitBytes] BETWEEN 0 AND 1000000000000000"));
        user.HasKey(x => x.Id);
        user.Property(x => x.Sid).HasMaxLength(184);
        user.HasIndex(x => x.Sid).IsUnique();
        user.Property(x => x.Account).HasMaxLength(256);
        user.Property(x => x.DisplayName).HasMaxLength(256);
        user.Property(x => x.Enabled).HasDefaultValue(true);
        user.Property(x => x.AdEnabled).HasDefaultValue(true);
        user.Property(x => x.AdAccount).HasMaxLength(64);
        user.Property(x => x.LocalAccount).HasMaxLength(64);
        user.Property(x => x.PasswordHash).HasMaxLength(512);
        user.Property(x => x.SecurityVersion).IsConcurrencyToken();
        user.HasIndex(x => x.AdAccount).IsUnique().HasFilter("[AdAccount] IS NOT NULL");
        user.HasIndex(x => x.LocalAccount).IsUnique().HasFilter("[LocalAccount] IS NOT NULL");
        user.OwnsOne(x => x.Preferences, p =>
        {
            p.ToTable("UserPreferences", "identity");
            p.WithOwner().HasForeignKey("UserId");
            p.Property<Guid>("UserId").HasComment("偏好所屬使用者的 Users 主鍵。");
            p.Property(x => x.Theme).HasMaxLength(12);
            p.Property(x => x.DefaultModelId).HasMaxLength(160);
            p.Property(x => x.Density).HasMaxLength(16);
            p.Property(x => x.ReadingWidth).HasMaxLength(16);
            p.Property(x => x.DefaultReasoningEffort).HasMaxLength(16);
            p.Property(x => x.Density).HasDefaultValue("comfortable");
            p.Property(x => x.ReadingWidth).HasDefaultValue("standard");
            p.Property(x => x.DefaultReasoningEffort).HasDefaultValue("auto");
            p.Property(x => x.ReadingFontSize).HasDefaultValue(UserPreferences.DefaultReadingFontSize);
            p.Property(x => x.ReadingLineHeight).HasDefaultValue(UserPreferences.DefaultReadingLineHeight);
            p.Property(x => x.SidebarWidth).HasDefaultValue(UserPreferences.DefaultSidebarWidth);
            p.Property(x => x.EnterToSend).HasDefaultValue(true);
            p.Property(x => x.AutoFollow).HasDefaultValue(true);
            p.Property(x => x.SaveLocalDrafts).HasDefaultValue(true);
        });
    }
}
