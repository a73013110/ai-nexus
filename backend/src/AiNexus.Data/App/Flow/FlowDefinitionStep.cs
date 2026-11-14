using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Flow;

/// <summary>
/// 流程的一關：誰來審由規則鍵＋JSON 參數決定（引擎呼叫對應的審核人規則），可設「某事實等於某值才需要」。
/// 欄位長度與申請送出時的快照 <see cref="FlowInstanceStep"/> 相同。
/// </summary>
public sealed class FlowDefinitionStep
{
    public const int MaxSteps = 20;
    public const int NameMaxLength = 60;
    public const int RuleMaxLength = 64;
    public const int ArgumentMaxLength = 2000;
    public const int FactMaxLength = 64;

    public string Kind { get; set; } = "";
    public int StepNumber { get; set; }
    public string Name { get; set; } = "";
    public string ApproverRule { get; set; } = "";
    public string? ApproverArgument { get; set; }

    /// <summary>與 <see cref="ConditionValue"/> 同時有值或同時為 null（<c>ck_FlowDefinitionSteps_Condition</c>）。</summary>
    public string? ConditionFact { get; set; }
    public string? ConditionValue { get; set; }
}

internal sealed class FlowDefinitionStepConfiguration : IEntityTypeConfiguration<FlowDefinitionStep>
{
    public void Configure(EntityTypeBuilder<FlowDefinitionStep> step)
    {
        step.ToTable("FlowDefinitionSteps", "flow");
        step.HasKey(x => new { x.Kind, x.StepNumber });
        step.Property(x => x.Kind).HasMaxLength(FlowDefinition.KindMaxLength);
        step.Property(x => x.Name).HasMaxLength(FlowDefinitionStep.NameMaxLength);
        step.Property(x => x.ApproverRule).HasMaxLength(FlowDefinitionStep.RuleMaxLength);
        step.Property(x => x.ApproverArgument).HasMaxLength(FlowDefinitionStep.ArgumentMaxLength);
        step.Property(x => x.ConditionFact).HasMaxLength(FlowDefinitionStep.FactMaxLength);
        step.Property(x => x.ConditionValue).HasMaxLength(FlowDefinitionStep.FactMaxLength);
        step.HasOne<FlowDefinition>().WithMany().HasForeignKey(x => x.Kind).OnDelete(DeleteBehavior.Restrict);
    }
}
