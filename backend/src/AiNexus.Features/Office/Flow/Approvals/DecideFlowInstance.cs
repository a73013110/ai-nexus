using AiNexus.Data.App;
using AiNexus.Data.App.Flow;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Validation;
using FluentValidation;

namespace AiNexus.Features.Office.Flow.Approvals;

/// <summary>對第 <paramref name="StepNumber"/> 關（畫面上看到的那一關）核准或退回；退回時建議填意見，申請人會收到。</summary>
public sealed record FlowDecisionRequest(int StepNumber, bool Approve, string? Comment);

internal sealed class FlowDecisionRequestValidator : RequestValidator<FlowDecisionRequest>
{
    public FlowDecisionRequestValidator()
    {
        RuleFor(x => x.StepNumber).InclusiveBetween(1, FlowDefinitionStep.MaxSteps).WithErrorCode(RuleCode.Range);
        RuleFor(x => x.Comment).OptionalContent(FlowInstanceStep.CommentMaxLength);
    }
}

/// <summary>審核目前這一關；審核人即時重算，不在名單內但持有 <c>flow.approvals.manage</c> 時記為代審。會稽核。</summary>
internal sealed class DecideFlowInstance(FlowEngine engine, AuditTrail audit) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapPost("/instances/{id:guid}/decision", (Guid id, FlowDecisionRequest body, ICurrentUser user, DecideFlowInstance handler, CancellationToken ct) =>
            handler.HandleAsync(id, body, user.Id, ct))
        .Audited(FlowAudit.InstanceReviewed)
        .WithName("DecideFlowInstance");

    public Task<Result> HandleAsync(Guid id, FlowDecisionRequest request, Guid actor, CancellationToken ct)
        => engine.ChangeAsync(id, async flow =>
        {
            var decided = await engine.DecideAsync(flow, request.StepNumber, actor, request.Approve, request.Comment, ct);
            audit.Describe(id, flow.Instance.ApplicantId, new
            {
                kind = flow.Instance.Kind, action = request.Approve ? FlowStepStatuses.Approved : FlowStepStatuses.Rejected,
                stepNumber = request.StepNumber, onBehalf = flow.Steps.SingleOrDefault(x => x.StepNumber == request.StepNumber)?.OnBehalf ?? false,
                status = flow.Instance.Status,
            });
            return decided;
        }, ct);
}
