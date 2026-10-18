using AiNexus.Data.App.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Administration;

public sealed class AdministratorBootstrap
{
    public Guid UserId { get; set; }
    public DateTimeOffset GrantedAt { get; set; }
}

internal sealed class AdministratorBootstrapConfiguration : IEntityTypeConfiguration<AdministratorBootstrap>
{
    public void Configure(EntityTypeBuilder<AdministratorBootstrap> bootstrap)
    {
        bootstrap.Property(x => x.GrantedAt).HasValueGenerator<CreationTime>();
        bootstrap.ToTable("AdministratorBootstraps", "administration");
        bootstrap.HasKey(x => x.UserId);
        bootstrap.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
