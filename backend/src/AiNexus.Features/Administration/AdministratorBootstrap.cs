using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Administration;

[Comment("管理者首次啟動授權的永久標記，防止撤銷後重新授權。")]
public sealed class AdministratorBootstrap
{
    [Comment("關聯使用者的 Users 主鍵。")]
    public Guid UserId { get; set; }
    [Comment("角色或資源授權建立時間。")]
    public DateTimeOffset GrantedAt { get; set; } = DateTimeOffset.UtcNow;
}

internal sealed class AdministratorBootstrapConfiguration : IEntityTypeConfiguration<AdministratorBootstrap>
{
    public void Configure(EntityTypeBuilder<AdministratorBootstrap> bootstrap)
    {
        bootstrap.ToTable("AdministratorBootstraps", "administration"); bootstrap.HasKey(x => x.UserId);
    }
}
