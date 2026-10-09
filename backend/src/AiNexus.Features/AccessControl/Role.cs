using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.AccessControl;

[Comment("可分派給使用者的角色；停用後不再提供有效授權。")]
public sealed class Role
{
    [Comment("資料的主鍵識別碼。")]
    public string Id { get; set; } = "";
    [Comment("業務物件的顯示名稱。")]
    public string Name { get; set; } = "";
    [Comment("是否啟用；停用不刪除歷史資料。")]
    public bool Enabled { get; set; } = true;
}

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> role)
    {
        role.ToTable("Roles", "accesscontrol");
        role.HasKey(x => x.Id);
        role.Property(x => x.Id).HasMaxLength(64);
        role.Property(x => x.Name).HasMaxLength(120);
        role.HasData(new Role { Id = BuiltInAccess.MemberRole, Name = "一般使用者" });
    }
}
