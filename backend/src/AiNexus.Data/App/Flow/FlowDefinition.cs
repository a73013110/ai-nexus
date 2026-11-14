using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Flow;

/// <summary>
/// 一種申請（kind）的簽核流程，一種一列（ADR 0036）；關卡在 <see cref="FlowDefinitionStep"/>，修改時整批取代並比對 <see cref="RowVersion"/>。
/// 種類由程式註冊，預設流程寫在 <c>Seed/flow.sql</c>。
/// </summary>
public sealed class FlowDefinition : IRowVersioned
{
    public const int KindMaxLength = 64;

    public string Kind { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public long RowVersion { get; set; }
}

internal sealed class FlowDefinitionConfiguration : IEntityTypeConfiguration<FlowDefinition>
{
    public void Configure(EntityTypeBuilder<FlowDefinition> definition)
    {
        definition.ToTable("FlowDefinitions", "flow");
        definition.HasKey(x => x.Kind);
        definition.Property(x => x.Kind).HasMaxLength(FlowDefinition.KindMaxLength);
        definition.Property(x => x.RowVersion).IsRowVersion().HasConversion<byte[]>();
    }
}
