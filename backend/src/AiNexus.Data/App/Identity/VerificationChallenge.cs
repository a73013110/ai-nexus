using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Identity;

/// <summary>
/// 一次性驗證碼：只存雜湊，到期、錯誤次數上限、成功使用或被新碼取代後即結束。
/// <c>MailMessageId</c> 刻意不設外鍵：郵件依保存期限刪除時不受阻擋，Identity 也不必認識 mail schema。
/// </summary>
public sealed class VerificationChallenge
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Purpose { get; set; } = "";
    public string? Target { get; set; }
    public byte[] CodeHash { get; set; } = [];
    public int Attempts { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? MailMessageId { get; set; }
}

internal sealed class VerificationChallengeConfiguration : IEntityTypeConfiguration<VerificationChallenge>
{
    public void Configure(EntityTypeBuilder<VerificationChallenge> challenge)
    {
        challenge.ToTable("VerificationChallenges", "identity");
        challenge.HasKey(x => x.Id);
        challenge.Property(x => x.Purpose).HasMaxLength(32);
        challenge.Property(x => x.Target).HasMaxLength(EmailSources.AddressMaxLength);
        challenge.Property(x => x.CodeHash).HasMaxLength(32).IsFixedLength();
        challenge.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
