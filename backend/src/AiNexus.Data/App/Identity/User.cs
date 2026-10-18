using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Identity;

public sealed class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Sid { get; set; } = "";
    public string Account { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public long? AttachmentLimitBytes { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public bool AdEnabled { get; set; } = true;
    public bool LocalEnabled { get; set; }
    public string? AdAccount { get; set; }
    public string? LocalAccount { get; set; }
    public string? PasswordHash { get; set; }
    public int SecurityVersion { get; set; }
    public bool ProfileManaged { get; set; }
    public int FailedLogins { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public UserPreferences Preferences { get; set; } = new();
}

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> user)
    {
        user.ToTable("Users", "identity");
        user.HasKey(x => x.Id);
        user.Property(x => x.Sid).HasMaxLength(184);
        user.Property(x => x.Account).HasMaxLength(256);
        user.Property(x => x.DisplayName).HasMaxLength(256);
        user.Property(x => x.AdAccount).HasMaxLength(64);
        user.Property(x => x.LocalAccount).HasMaxLength(64);
        user.Property(x => x.PasswordHash).HasMaxLength(512);
        user.Property(x => x.SecurityVersion).IsConcurrencyToken();
        user.OwnsOne(x => x.Preferences, p =>
        {
            p.ToTable("UserPreferences", "identity");
            p.WithOwner().HasForeignKey("UserId");
            p.Property(x => x.Theme).HasMaxLength(12);
            p.Property(x => x.DefaultModelId).HasMaxLength(160);
            p.Property(x => x.Density).HasMaxLength(16);
            p.Property(x => x.ReadingWidth).HasMaxLength(16);
            p.Property(x => x.DefaultReasoningEffort).HasMaxLength(16);
        });
    }
}
