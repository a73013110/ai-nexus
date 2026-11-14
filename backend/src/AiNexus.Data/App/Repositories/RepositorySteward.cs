using AiNexus.Data.App.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Repositories;

/// <summary>儲存庫負責人：可直接授予、撤銷與改到期日，也是審核人規則 <c>repository-stewards</c> 的來源（ADR 0037）。</summary>
public sealed class RepositorySteward
{
    public Guid RepositoryId { get; set; }
    public Guid UserId { get; set; }
}

internal sealed class RepositoryStewardConfiguration : IEntityTypeConfiguration<RepositorySteward>
{
    public void Configure(EntityTypeBuilder<RepositorySteward> steward)
    {
        steward.ToTable("RepositoryStewards", "repositories");
        steward.HasKey(x => new { x.RepositoryId, x.UserId });
        steward.HasOne<ManagedRepository>().WithMany().HasForeignKey(x => x.RepositoryId).OnDelete(DeleteBehavior.Restrict);
        steward.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
