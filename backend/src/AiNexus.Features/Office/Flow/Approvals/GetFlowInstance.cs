using AiNexus.Data.App;
using AiNexus.Data.App.Flow;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Office.Flow.Approvals;

/// <summary>一關的狀態與決定；<paramref name="Note"/> 是自動通過或略過的原因（<c>FlowStepNotes</c>）。</summary>
public sealed record FlowStepDto(int StepNumber, string Name, string Status, string? Note, bool Fallback, DateTimeOffset? ActivatedAt,
    DateTimeOffset? DecidedAt, FlowPersonDto? Decider, bool OnBehalf, string? Comment);

/// <summary>
/// 申請詳情。<paramref name="Approvers"/> 是目前這一關即時計算的審核人；<paramref name="CanDecide"/> 時
/// <paramref name="OnBehalf"/> 表示會記為代審；決定要帶回 <paramref name="CurrentStep"/>。
/// </summary>
public sealed record FlowInstanceDto(Guid Id, string Kind, string KindName, string Title, IReadOnlyList<FlowDetail> Details, FlowPersonDto Applicant,
    string Status, int? CurrentStep, DateTimeOffset SubmittedAt, DateTimeOffset? CompletedAt, IReadOnlyList<FlowStepDto> Steps,
    IReadOnlyList<FlowPersonDto> Approvers, bool CanDecide, bool OnBehalf, bool CanCancel);

/// <summary>
/// 申請人、目前的審核人、審過的人與持有 <c>flow.approvals.manage</c> 的人看得到；其他人一律回找不到，不透露申請是否存在。
/// </summary>
internal sealed class GetFlowInstance(AppDbContext db, FlowEngine engine, FlowCatalog catalog, AccessService access, AuditTrail audit) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapGet("/instances/{id:guid}", (Guid id, ICurrentUser user, GetFlowInstance handler, CancellationToken ct) => handler.HandleAsync(id, user.Id, ct))
        .Audited(FlowAudit.InstanceRead)
        .WithName("GetFlowInstance");

    public async Task<Result<FlowInstanceDto>> HandleAsync(Guid id, Guid actor, CancellationToken ct)
    {
        var flow = await engine.LoadAsync(id, ct);
        if (flow is null) return FlowErrors.InstanceNotFound;
        var instance = flow.Instance;
        var approvers = flow.Current is { } current ? (await engine.ApproversAsync(instance, current, ct)).Users : new HashSet<Guid>();
        var manager = await access.HoldsAsync(actor, FeatureCatalog.FlowApprovalsManage, ct);
        var visible = actor == instance.ApplicantId || approvers.Contains(actor) || manager || flow.Steps.Any(x => x.DeciderId == actor)
            || await db.FlowAssignments.AnyAsync(x => x.InstanceId == id && x.UserId == actor, ct);
        if (!visible) return FlowErrors.InstanceNotFound;
        audit.Describe(id, instance.ApplicantId, new { kind = instance.Kind });

        var people = flow.Steps.Where(x => x.DeciderId is not null).Select(x => x.DeciderId!.Value).Append(instance.ApplicantId).Concat(approvers).Distinct().ToArray();
        var names = await db.Users.AsNoTracking().Where(x => people.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.DisplayName, ct);
        FlowPersonDto Person(Guid user) => new(user, names.GetValueOrDefault(user) ?? "（已刪除的使用者）");

        var pending = instance.Status == FlowStatuses.Pending;
        var canDecide = pending && actor != instance.ApplicantId && (approvers.Contains(actor) || manager);
        return new FlowInstanceDto(instance.Id, instance.Kind, catalog.KindName(instance.Kind), instance.Title, FlowDetail.Parse(instance.Details),
            Person(instance.ApplicantId), instance.Status, instance.CurrentStep, instance.SubmittedAt, instance.CompletedAt,
            [.. flow.Steps.Select(x => new FlowStepDto(x.StepNumber, x.Name, x.Status, x.Note, x.Fallback, x.ActivatedAt, x.DecidedAt,
                x.DeciderId is { } decider ? Person(decider) : null, x.OnBehalf, x.Comment))],
            [.. approvers.Select(Person).OrderBy(x => x.DisplayName, StringComparer.Ordinal)],
            canDecide, canDecide && !approvers.Contains(actor), pending && actor == instance.ApplicantId);
    }
}
