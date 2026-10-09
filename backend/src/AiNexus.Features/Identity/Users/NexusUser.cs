using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Identity.Users;

public sealed class NexusUser
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
