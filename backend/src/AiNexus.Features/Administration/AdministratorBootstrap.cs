using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Administration;

public sealed class AdministratorBootstrap
{
    public Guid UserId { get; set; }
    public DateTimeOffset GrantedAt { get; set; } = DateTimeOffset.UtcNow;
}

internal sealed class AdministratorBootstrapConfiguration : IEntityTypeConfiguration<AdministratorBootstrap>
{
    public void Configure(EntityTypeBuilder<AdministratorBootstrap> bootstrap)
    {
        bootstrap.ToTable("AdministratorBootstraps", "access"); bootstrap.HasKey(x => x.UserId);
    }
}
