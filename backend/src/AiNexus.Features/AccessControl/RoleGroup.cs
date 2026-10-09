using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.AccessControl;

[Comment("角色所加入的功能群組，集中管理功能及模型政策。")]
public sealed class RoleGroup
{
    [Comment("資料的主鍵識別碼。")]
    public string Id { get; set; } = "";
    [Comment("業務物件的顯示名稱。")]
    public string Name { get; set; } = "";
    [Comment("是否啟用；停用不刪除歷史資料。")]
    public bool Enabled { get; set; } = true;
}

internal sealed class RoleGroupConfiguration : IEntityTypeConfiguration<RoleGroup>
{
    public void Configure(EntityTypeBuilder<RoleGroup> group)
    {
        group.ToTable("RoleGroups", "accesscontrol");
        group.HasKey(x => x.Id);
        group.Property(x => x.Id).HasMaxLength(64);
        group.Property(x => x.Name).HasMaxLength(120);
        group.HasData(new RoleGroup { Id = BuiltInAccess.WorkspaceGroup, Name = "基本工作區" });
    }
}
