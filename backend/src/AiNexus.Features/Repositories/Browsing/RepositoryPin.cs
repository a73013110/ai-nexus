using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Repositories.Browsing;

/// <summary>使用者釘在清單頂端的程式庫；名稱與說明是釘選當時 Gitea 顯示的內容。</summary>
[Comment("使用者釘選的 Gitea 程式庫；名稱與說明為釘選當時的快照。")]
public sealed class RepositoryPin
{
    public const int Limit = 30;

    [Comment("資料的主鍵識別碼。")]
    public Guid Id { get; set; } = Guid.NewGuid();
    [Comment("資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。")]
    public Guid OwnerId { get; set; }
    [Comment("Gitea 連線主機位址。")]
    public string BaseUrl { get; set; } = "";
    [Comment("Gitea repository 的 owner/name 識別。")]
    public string Repository { get; set; } = "";
    [Comment("釘選當時的程式庫說明。")]
    public string Description { get; set; } = "";
    [Comment("釘選當時是否為私人程式庫。")]
    public bool Private { get; set; }
    [Comment("釘選的時間。")]
    public DateTimeOffset PinnedAt { get; set; }
}

internal sealed class RepositoryPinConfiguration : IEntityTypeConfiguration<RepositoryPin>
{
    public void Configure(EntityTypeBuilder<RepositoryPin> p)
    {
        p.ToTable("RepositoryPins", "repositories"); p.HasKey(x => x.Id); p.HasIndex(x => new { x.OwnerId, x.BaseUrl, x.Repository }).IsUnique();
        p.Property(x => x.BaseUrl).HasMaxLength(500); p.Property(x => x.Repository).HasMaxLength(201); p.Property(x => x.Description).HasMaxLength(500);
        p.Property(x => x.PinnedAt).HasValueGenerator<Persistence.CreationTime>();
    }
}
