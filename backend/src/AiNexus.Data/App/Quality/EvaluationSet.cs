using AiNexus.Data.App.Collaboration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Quality;

/// <summary>版本化的題庫。名稱、擁有者與分享設定存放在相同 id 的 <see cref="Resource"/>，這裡只存題目與版本。</summary>
public sealed class EvaluationSet
{
    public const string Kind = "evaluation";

    public Guid Id { get; set; }
    public string Description { get; set; } = "";
    public string CasesJson { get; set; } = "[]";
    public int Version { get; set; } = 1;
}

internal sealed class EvaluationSetConfiguration : IEntityTypeConfiguration<EvaluationSet>
{
    public void Configure(EntityTypeBuilder<EvaluationSet> s)
    {
        s.ToTable("EvaluationSets", "quality");
        s.HasKey(x => x.Id);
        s.Property(x => x.Description).HasMaxLength(2000);
        s.HasOne<Resource>().WithMany().HasForeignKey(x => x.Id).OnDelete(DeleteBehavior.Restrict);
    }
}
