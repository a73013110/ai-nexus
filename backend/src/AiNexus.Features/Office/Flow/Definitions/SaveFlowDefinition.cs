using AiNexus.Data.App;
using AiNexus.Data.App.Flow;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Office.Flow.Definitions;

/// <summary>一關的設定；條件的事實與值要同時填或同時留空。參數是規則自己的 JSON，沒有參數的規則留空。</summary>
public sealed record FlowStepRequest(string Name, string ApproverRule, string? ApproverArgument, string? ConditionFact, string? ConditionValue);

/// <summary>整條流程依序覆寫；<paramref name="RowVersion"/> 為 null 表示建立（還沒有流程設定的種類）。</summary>
public sealed record FlowDefinitionRequest(IReadOnlyList<FlowStepRequest> Steps, long? RowVersion);

internal sealed class FlowDefinitionRequestValidator : RequestValidator<FlowDefinitionRequest>
{
    public FlowDefinitionRequestValidator()
    {
        RuleFor(x => x.Steps).NotNull().WithErrorCode(RuleCode.Required)
            .Must(x => x is null or { Count: >= 1 and <= FlowDefinitionStep.MaxSteps }).WithErrorCode(RuleCode.Count);
        RuleForEach(x => x.Steps).ChildRules(step =>
        {
            step.RuleFor(x => x.Name).RequiredText(FlowDefinitionStep.NameMaxLength);
            step.RuleFor(x => x.ApproverRule).RequiredText(FlowDefinitionStep.RuleMaxLength);
            step.RuleFor(x => x.ApproverArgument).OptionalText(FlowDefinitionStep.ArgumentMaxLength);
            step.RuleFor(x => x.ConditionFact).OptionalText(FlowDefinitionStep.FactMaxLength);
            step.RuleFor(x => x.ConditionValue).OptionalText(FlowDefinitionStep.FactMaxLength)
                .Must((x, value) => string.IsNullOrWhiteSpace(x.ConditionFact) == string.IsNullOrWhiteSpace(value)).WithErrorCode(RuleCode.Invalid);
        });
        RuleFor(x => x.RowVersion).GreaterThan(0).WithErrorCode(RuleCode.Required).When(x => x.RowVersion is not null);
    }
}

/// <summary>
/// 修改一種申請的流程。只影響之後送出的申請：進行中的申請用送出時的快照。規則、參數與條件在這裡檢查（資料庫不認得 JSON），
/// 規則限定種類的（如儲存庫負責人）不能用在別的種類。會稽核前後的關卡。
/// </summary>
internal sealed class SaveFlowDefinition(AppDbContext db, AdministrativeWrite write, FlowCatalog catalog, KeyedServices<IApproverRule> rules, TimeProvider clock) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapPut("/{kind}", (string kind, FlowDefinitionRequest body, SaveFlowDefinition handler, CancellationToken ct) => handler.HandleAsync(kind, body, ct))
        .Audited(FlowAudit.DefinitionUpdated)
        .WithName("SaveFlowDefinition");

    public async Task<Result> HandleAsync(string kindKey, FlowDefinitionRequest request, CancellationToken ct)
    {
        if (catalog.Kind(kindKey) is not { } kind) return FlowErrors.KindNotFound;
        var steps = request.Steps.Select((x, i) => new FlowDefinitionStep
        {
            Kind = kind.Kind, StepNumber = i + 1, Name = x.Name.Trim(), ApproverRule = x.ApproverRule.Trim(), ApproverArgument = x.ApproverArgument.TrimToNull(),
            ConditionFact = x.ConditionFact.TrimToNull(), ConditionValue = x.ConditionValue.TrimToNull(),
        }).ToList();
        foreach (var step in steps)
            if (await CheckAsync(kind, step, ct) is { } invalid) return invalid;

        var begun = await write.BeginAsync(AuditTarget.Of(kind.Kind, token => SnapshotAsync(db, kind.Kind, token)), ct);
        if (!begun.IsSuccess) return begun.Error;
        await using var transaction = begun.Value;

        var definition = await db.FlowDefinitions.SingleOrDefaultAsync(x => x.Kind == kind.Kind, ct);
        var now = clock.GetUtcNow();
        if (request.RowVersion is { } version)
        {
            if (definition is null) return FlowErrors.DefinitionMissing;
            db.ExpectRowVersion(definition, version);
            definition.UpdatedAt = now;
        }
        else if (definition is not null) return AccessControlErrors.WriteConflict;
        else db.FlowDefinitions.Add(new FlowDefinition { Kind = kind.Kind, UpdatedAt = now });

        // 依序號就地更新、刪多的、補少的：主鍵是（種類, 序號），不先刪再加同一個鍵。
        var existing = await db.FlowDefinitionSteps.Where(x => x.Kind == kind.Kind).ToDictionaryAsync(x => x.StepNumber, ct);
        db.FlowDefinitionSteps.RemoveRange(existing.Values.Where(x => x.StepNumber > steps.Count));
        foreach (var step in steps)
        {
            if (!existing.TryGetValue(step.StepNumber, out var row)) { db.FlowDefinitionSteps.Add(step); continue; }
            (row.Name, row.ApproverRule, row.ApproverArgument, row.ConditionFact, row.ConditionValue) =
                (step.Name, step.ApproverRule, step.ApproverArgument, step.ConditionFact, step.ConditionValue);
        }
        return await transaction.CommitAsync(ct);
    }

    /// <summary>規則存在且允許這個種類、參數通過規則的檢查、條件引用種類宣告過的事實與值。</summary>
    private async Task<Error?> CheckAsync(FlowKindDefinition kind, FlowDefinitionStep step, CancellationToken ct)
    {
        if (catalog.Rule(step.ApproverRule) is not { } rule || !rule.AllowedFor(kind.Kind)) return FlowErrors.RuleInvalid;
        if (rule.Argument == ApproverArguments.None ? step.ApproverArgument is not null : await rules.Require(rule.Key).ValidateAsync(step.ApproverArgument, ct) is not null)
            return FlowErrors.ArgumentInvalid;
        if (step.ConditionFact is { } fact && kind.Facts.SingleOrDefault(x => x.Key == fact)?.Values.Any(x => x.Value == step.ConditionValue) != true)
            return FlowErrors.ConditionInvalid;
        return null;
    }

    private static async Task<object?> SnapshotAsync(AppDbContext db, string kind, CancellationToken ct) => new
    {
        steps = await db.FlowDefinitionSteps.Where(x => x.Kind == kind).OrderBy(x => x.StepNumber)
            .Select(x => new { x.StepNumber, x.Name, x.ApproverRule, x.ApproverArgument, x.ConditionFact, x.ConditionValue }).ToListAsync(ct),
    };
}
