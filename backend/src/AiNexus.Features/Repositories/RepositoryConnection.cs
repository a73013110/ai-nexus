using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Repositories;

/// <summary>A user's own Gitea token, encrypted for that user and host. Only <see cref="RepositoryService"/> decrypts it.</summary>
[Comment("使用者個人的 Gitea 連線及 Data Protection 保護的存取 token。")]
public sealed class RepositoryConnection
{
    [Comment("資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。")]
    public Guid OwnerId { get; set; }
    [Comment("Gitea 連線主機位址。")]
    public string BaseUrl { get; set; } = "";
    [Comment("外部服務的使用者登入名稱。")]
    public string Login { get; set; } = "";
    [Comment("以 ASP.NET Data Protection 保護的外部 token；不可在 API、稽核或日誌回傳。")]
    public string ProtectedToken { get; set; } = "";
    [Comment("使用者建立外部服務連線的時間。")]
    public DateTimeOffset ConnectedAt { get; set; }
}

internal sealed class RepositoryConnectionConfiguration : IEntityTypeConfiguration<RepositoryConnection>
{
    public void Configure(EntityTypeBuilder<RepositoryConnection> c)
    {
        c.Property(x => x.ConnectedAt).HasValueGenerator<CreationTime>();
        c.ToTable("RepositoryConnections", "repositories"); c.HasKey(x => x.OwnerId);
        c.Property(x => x.BaseUrl).HasMaxLength(500); c.Property(x => x.Login).HasMaxLength(100); c.Property(x => x.ProtectedToken).HasMaxLength(4096);
    }
}
