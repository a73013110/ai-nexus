using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Repositories;

/// <summary>A user's own Gitea token, encrypted for that user and host. Only <see cref="RepositoryService"/> decrypts it.</summary>
public sealed class RepositoryConnection
{
    public Guid OwnerId { get; set; }
    public string BaseUrl { get; set; } = "";
    public string Login { get; set; } = "";
    public string ProtectedToken { get; set; } = "";
    public DateTimeOffset ConnectedAt { get; set; } = DateTimeOffset.UtcNow;
}

internal sealed class RepositoryConnectionConfiguration : IEntityTypeConfiguration<RepositoryConnection>
{
    public void Configure(EntityTypeBuilder<RepositoryConnection> c)
    {
        c.ToTable("RepositoryConnections", "workspace"); c.HasKey(x => x.OwnerId);
        c.Property(x => x.BaseUrl).HasMaxLength(500); c.Property(x => x.Login).HasMaxLength(100); c.Property(x => x.ProtectedToken).HasMaxLength(4096);
    }
}
