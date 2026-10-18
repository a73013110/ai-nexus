using AiNexus.Data.App.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Repositories;

/// <summary>使用者釘在清單頂端的程式庫；名稱與說明是釘選當時 Gitea 顯示的內容。</summary>
public sealed class RepositoryPin
{
    public const int Limit = 30;

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public string BaseUrl { get; set; } = "";
    public string Repository { get; set; } = "";
    public string Description { get; set; } = "";
    public bool Private { get; set; }
    public DateTimeOffset PinnedAt { get; set; }
}

internal sealed class RepositoryPinConfiguration : IEntityTypeConfiguration<RepositoryPin>
{
    public void Configure(EntityTypeBuilder<RepositoryPin> p)
    {
        p.ToTable("RepositoryPins", "repositories");
        p.HasKey(x => x.Id);
        p.Property(x => x.BaseUrl).HasMaxLength(500);
        p.Property(x => x.Repository).HasMaxLength(201);
        p.Property(x => x.Description).HasMaxLength(500);
        p.Property(x => x.PinnedAt).HasValueGenerator<CreationTime>();
        p.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
    }
}
