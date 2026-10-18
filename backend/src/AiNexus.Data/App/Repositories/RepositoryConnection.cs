using AiNexus.Data.App.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Repositories;

/// <summary>使用者自己的 Gitea token，以該使用者與主機為範圍加密；只有 <c>RepositoryService</c> 會解密。</summary>
public sealed class RepositoryConnection
{
    public Guid OwnerId { get; set; }
    public string BaseUrl { get; set; } = "";
    public string Login { get; set; } = "";
    public string ProtectedToken { get; set; } = "";
    public DateTimeOffset ConnectedAt { get; set; }
}

internal sealed class RepositoryConnectionConfiguration : IEntityTypeConfiguration<RepositoryConnection>
{
    public void Configure(EntityTypeBuilder<RepositoryConnection> c)
    {
        c.Property(x => x.ConnectedAt).HasValueGenerator<CreationTime>();
        c.ToTable("RepositoryConnections", "repositories");
        c.HasKey(x => x.OwnerId);
        c.Property(x => x.BaseUrl).HasMaxLength(500);
        c.Property(x => x.Login).HasMaxLength(100);
        c.Property(x => x.ProtectedToken).HasMaxLength(4096);
        c.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
    }
}
