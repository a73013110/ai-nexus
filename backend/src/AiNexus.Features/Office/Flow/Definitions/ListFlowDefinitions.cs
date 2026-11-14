using AiNexus.Data.App;
using AiNexus.Features.Office.Flow.Approvals;
using AiNexus.Features.Office.Flow.Rules;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Office.Flow.Definitions;

public sealed record FlowStepDefinitionDto(string Name, string ApproverRule, string? ApproverArgument, string? ConditionFact, string? ConditionValue);

/// <summary>一種申請與它的流程；<paramref name="RowVersion"/> 為 null 表示還沒有流程設定（缺種子），儲存時建立。</summary>
public sealed record FlowDefinitionDto(string Kind, string Name, string Description, IReadOnlyList<FlowFact> Facts,
    IReadOnlyList<FlowStepDefinitionDto> Steps, DateTimeOffset? UpdatedAt, long? RowVersion);

public sealed record ApproverRuleDto(string Key, string Name, string Description, string Argument, IReadOnlyList<string> Kinds);

/// <summary><paramref name="People"/> 是「指定人員」形式的參數引用到的人，設定畫面據此顯示姓名。</summary>
public sealed record FlowDefinitionsDto(IReadOnlyList<FlowDefinitionDto> Kinds, IReadOnlyList<ApproverRuleDto> Rules, IReadOnlyList<FlowPersonDto> People);

/// <summary>已註冊的申請種類與它們的流程，以及可用的審核人規則；程式不認得的舊種類不列出。</summary>
internal sealed class ListFlowDefinitions(AppDbContext db, FlowCatalog catalog) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapGet("", (ListFlowDefinitions handler, CancellationToken ct) => handler.HandleAsync(ct))
        .Audited(FlowAudit.DefinitionRead)
        .WithName("ListFlowDefinitions");

    public async Task<Result<FlowDefinitionsDto>> HandleAsync(CancellationToken ct)
    {
        var definitions = await db.FlowDefinitions.AsNoTracking().ToDictionaryAsync(x => x.Kind, ct);
        var steps = (await db.FlowDefinitionSteps.AsNoTracking().OrderBy(x => x.StepNumber).ToListAsync(ct)).ToLookup(x => x.Kind);
        var referenced = steps.SelectMany(x => x)
            .Where(x => catalog.Rule(x.ApproverRule)?.Argument == ApproverArguments.Users)
            .SelectMany(x => ApproverArgument.UserIds(x.ApproverArgument) ?? []).Distinct().ToArray();
        var people = await db.Users.AsNoTracking().Where(x => referenced.Contains(x.Id))
            .OrderBy(x => x.DisplayName).Select(x => new FlowPersonDto(x.Id, x.DisplayName)).ToListAsync(ct);
        return new FlowDefinitionsDto(
            [.. catalog.Kinds.Select(kind => new FlowDefinitionDto(kind.Kind, kind.Name, kind.Description, kind.Facts,
                [.. steps[kind.Kind].Select(x => new FlowStepDefinitionDto(x.Name, x.ApproverRule, x.ApproverArgument, x.ConditionFact, x.ConditionValue))],
                definitions.GetValueOrDefault(kind.Kind)?.UpdatedAt, definitions.GetValueOrDefault(kind.Kind)?.RowVersion))],
            [.. catalog.Rules.Select(x => new ApproverRuleDto(x.Key, x.Name, x.Description, x.Argument, x.Kinds))], people);
    }
}
