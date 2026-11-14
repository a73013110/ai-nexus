using AiNexus.Data.App;
using AiNexus.Data.App.Flow;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Strings;
using AiNexus.Platform.Validation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Office.Flow;

/// <summary>一筆申請與它的關卡（追蹤中），引擎操作的單位。</summary>
public sealed record FlowCase(FlowInstance Instance, IReadOnlyList<FlowInstanceStep> Steps)
{
    /// <summary>審核中的關卡；申請已結束時為 null。</summary>
    public FlowInstanceStep? Current => Instance.CurrentStep is { } number ? Steps.SingleOrDefault(x => x.StepNumber == number) : null;
}

/// <summary>一關的審核人；<paramref name="Fallback"/> 表示規則算不出人，改由持有 <c>flow.approvals.manage</c> 的人審核。</summary>
public sealed record FlowApprovers(IReadOnlySet<Guid> Users, bool Fallback);

/// <summary>
/// 簽核引擎（ADR 0036）：送出、決定、取消、逾期與指派重算都經過這裡，規則只寫一次。
/// 每關任一人核准即通過、任一關退回即結束；申請人不審自己；算不出人時交給簽核管理員；
/// 前面關卡已核准的人也是這關的審核人時自動通過；條件不成立的關卡在送出時就記為不適用。
/// </summary>
[Scoped]
public sealed partial class FlowEngine(
    AppDbContext db, FlowCatalog catalog, KeyedServices<IApproverRule> rules, KeyedServices<IFlowKind> kinds,
    AccessService access, IActiveUsers activeUsers, FlowNotices notices, TimeProvider clock, ILogger<FlowEngine> logger)
{
    private static readonly IReadOnlySet<Guid> Nobody = new HashSet<Guid>();

    internal async Task<FlowCase?> LoadAsync(Guid id, CancellationToken ct)
    {
        var instance = await db.FlowInstances.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (instance is null) return null;
        var steps = await db.FlowInstanceSteps.Where(x => x.InstanceId == id).OrderBy(x => x.StepNumber).ToListAsync(ct);
        return new(instance, steps);
    }

    /// <summary>見 <see cref="FlowService.SubmitAsync"/>。種類未註冊、事實未宣告是程式錯誤，直接拋出。</summary>
    internal async Task<Result<FlowInstance>> StartAsync(FlowSubmission submission, CancellationToken ct)
    {
        var kind = catalog.Kind(submission.Kind) ?? throw new InvalidOperationException($"Flow kind '{submission.Kind}' is not registered.");
        foreach (var (key, value) in submission.Facts)
            if (kind.Facts.SingleOrDefault(x => x.Key == key)?.Values.Any(x => x.Value == value) != true)
                throw new InvalidOperationException($"Flow kind '{kind.Kind}' does not declare fact '{key}={value}'.");
        var details = FlowDetail.Serialize(submission.Details);
        if (details.Length > FlowInstance.DetailsMaxLength)
            throw new InvalidOperationException($"Flow details for '{kind.Kind}' exceed {FlowInstance.DetailsMaxLength} characters.");
        if (!await db.FlowDefinitions.AnyAsync(x => x.Kind == kind.Kind, ct)) return FlowErrors.DefinitionMissing;
        var definition = await db.FlowDefinitionSteps.AsNoTracking().Where(x => x.Kind == kind.Kind).OrderBy(x => x.StepNumber).ToListAsync(ct);

        var now = clock.GetUtcNow();
        var instance = new FlowInstance
        {
            Kind = kind.Kind, SubjectId = submission.SubjectId, ApplicantId = submission.ApplicantId,
            Title = submission.Title.Clip(FlowInstance.TitleMaxLength), Details = details, SubmittedAt = now,
        };
        var steps = definition.Select(x => Applies(x, submission.Facts)
            ? new FlowInstanceStep { InstanceId = instance.Id, StepNumber = x.StepNumber, Name = x.Name, ApproverRule = x.ApproverRule, ApproverArgument = x.ApproverArgument }
            : new FlowInstanceStep { InstanceId = instance.Id, StepNumber = x.StepNumber, Name = x.Name, ApproverRule = x.ApproverRule, ApproverArgument = x.ApproverArgument, Status = FlowStepStatuses.Skipped, Note = FlowStepNotes.NotApplicable }).ToList();
        db.FlowInstances.Add(instance);
        db.FlowInstanceSteps.AddRange(steps);
        var advanced = await AdvanceAsync(new(instance, steps), null, now, ct);
        return advanced.IsSuccess ? instance : advanced.Error;
    }

    /// <summary>
    /// 在自己的交易內載入申請、套用 <paramref name="change"/> 並提交。失敗或與其他決定撞上（<c>flow_instance_changed</c>）時整筆回滾：
    /// 同一筆申請的變更都會更新申請列，並行控制欄位讓後到的那個失敗，不會兩個人同時通過同一關。
    /// </summary>
    internal async Task<Result> ChangeAsync(Guid id, Func<FlowCase, Task<Result>> change, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var flow = await LoadAsync(id, ct);
        if (flow is null) return FlowErrors.InstanceNotFound;
        var result = await change(flow);
        if (!result.IsSuccess)
        {
            db.ChangeTracker.Clear();
            return result;
        }
        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Result.Success;
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return FlowErrors.InstanceChanged;
        }
    }

    /// <summary>
    /// <paramref name="actor"/> 對第 <paramref name="stepNumber"/> 關（畫面上看到的那一關）的決定。審核人即時重算，不信任指派快取；
    /// 不在名單內但持有 <c>flow.approvals.manage</c> 的人可以代審。關卡已換過時回 <c>flow_instance_changed</c>，讓人看過新狀態再決定。
    /// </summary>
    internal async Task<Result> DecideAsync(FlowCase flow, int stepNumber, Guid actor, bool approve, string? comment, CancellationToken ct)
    {
        var instance = flow.Instance;
        if (instance.Status != FlowStatuses.Pending || flow.Current is not { } step) return FlowErrors.InstanceClosed;
        if (step.StepNumber != stepNumber) return FlowErrors.InstanceChanged;
        if (actor == instance.ApplicantId) return FlowErrors.OwnRequest;
        var approvers = await ApproversAsync(instance, step, ct);
        var onBehalf = !approvers.Users.Contains(actor);
        if (onBehalf && !await access.HoldsAsync(actor, FeatureCatalog.FlowApprovalsManage, ct)) return FlowErrors.NotApprover;

        var now = clock.GetUtcNow();
        step.Status = approve ? FlowStepStatuses.Approved : FlowStepStatuses.Rejected;
        (step.DecidedAt, step.DeciderId, step.OnBehalf) = (now, actor, onBehalf);
        step.Comment = comment.TrimToNull();
        await notices.ResolveAsync(instance.Id, ct);
        return approve ? await AdvanceAsync(flow, actor, now, ct) : await FinishAsync(flow, FlowStatuses.Rejected, actor, now, ct);
    }

    /// <summary>取消（申請人）或逾期（排程，<paramref name="actor"/> 為 null）。</summary>
    internal async Task<Result> CloseAsync(FlowCase flow, string status, Guid? actor, CancellationToken ct)
    {
        if (flow.Instance.Status != FlowStatuses.Pending) return FlowErrors.InstanceClosed;
        await notices.ResolveAsync(flow.Instance.Id, ct);
        return await FinishAsync(flow, status, actor, clock.GetUtcNow(), ct);
    }

    /// <summary>
    /// 組織或授權異動後重算目前關卡的指派，新加入的人收到通知；指派沒變就什麼也不寫。
    /// 有變時一併更新申請列：與同時進行的決定交錯時，後提交的一方因並行控制而失敗，不會留下已結束關卡的指派。
    /// </summary>
    internal async Task<int> RefreshAsync(FlowCase flow, CancellationToken ct)
    {
        if (flow.Instance.Status != FlowStatuses.Pending || flow.Current is not { } step) return 0;
        var approvers = await ApproversAsync(flow.Instance, step, ct);
        var (added, removed) = await AssignAsync(flow.Instance.Id, approvers.Users, ct);
        if (added.Count == 0 && removed == 0) return 0;
        step.Fallback = approvers.Fallback;
        db.ExpectRowVersion(flow.Instance, flow.Instance.RowVersion);
        await notices.AssignedAsync(flow, step, added, ct);
        return added.Count + removed;
    }

    /// <summary>一關目前的審核人：規則的候選人去掉申請人與停用帳號；沒有人時改為簽核管理員（同樣去掉申請人）。</summary>
    internal async Task<FlowApprovers> ApproversAsync(FlowInstance instance, FlowInstanceStep step, CancellationToken ct)
    {
        var rule = rules.Find(step.ApproverRule);
        if (rule is null) LogRuleMissing(logger, instance.Id, step.ApproverRule);
        var candidates = rule is null ? [] : await rule.ResolveAsync(new(instance.Kind, instance.SubjectId, instance.ApplicantId, step.ApproverArgument), ct);
        var users = await EligibleAsync(candidates, instance.ApplicantId, ct);
        if (users.Count > 0) return new(users, false);
        var fallback = await EligibleAsync(await access.HoldersAsync(FeatureCatalog.FlowApprovalsManage, ct), instance.ApplicantId, ct);
        if (fallback.Count == 0) LogNobody(logger, instance.Id, step.StepNumber);
        return new(fallback, true);
    }

    /// <summary>從第一個還在等待的關卡往下找：自動通過的繼續，遇到要人審的就啟動並停下；全部走完即核准。</summary>
    private async Task<Result> AdvanceAsync(FlowCase flow, Guid? actor, DateTimeOffset now, CancellationToken ct)
    {
        foreach (var step in flow.Steps.Where(x => x.Status == FlowStepStatuses.Waiting))
        {
            var approvers = await ApproversAsync(flow.Instance, step, ct);
            (step.ActivatedAt, step.Fallback) = (now, approvers.Fallback);
            // 同一人不連按兩次：前面核准過的人也是這關的審核人，就算他已經同意。
            if (flow.Steps.FirstOrDefault(x => x.Status == FlowStepStatuses.Approved && x.DeciderId is { } decider && approvers.Users.Contains(decider)) is { } earlier)
            {
                (step.Status, step.Note, step.DeciderId, step.DecidedAt) = (FlowStepStatuses.Approved, FlowStepNotes.AlreadyApproved, earlier.DeciderId, now);
                actor = earlier.DeciderId;
                continue;
            }
            step.Status = FlowStepStatuses.Active;
            flow.Instance.CurrentStep = step.StepNumber;
            await AssignAsync(flow.Instance.Id, approvers.Users, ct);
            await notices.AssignedAsync(flow, step, approvers.Users, ct);
            return Result.Success;
        }
        return await FinishAsync(flow, FlowStatuses.Approved, actor, now, ct);
    }

    /// <summary>結束申請並呼叫種類的 <see cref="IFlowKind.CompleteAsync"/>；種類已移除時，除了核准都照常結束。</summary>
    private async Task<Result> FinishAsync(FlowCase flow, string status, Guid? actor, DateTimeOffset now, CancellationToken ct)
    {
        var instance = flow.Instance;
        (instance.Status, instance.CurrentStep, instance.CompletedAt) = (status, null, now);
        foreach (var step in flow.Steps.Where(x => x.Status is FlowStepStatuses.Waiting or FlowStepStatuses.Active)) step.Status = FlowStepStatuses.Stopped;
        await AssignAsync(instance.Id, Nobody, ct);
        if (kinds.Find(instance.Kind) is { } kind)
        {
            var completed = await kind.CompleteAsync(new(instance.Id, instance.Kind, instance.SubjectId, instance.ApplicantId, status, actor), ct);
            if (!completed.IsSuccess) return completed.Error;
        }
        else if (status == FlowStatuses.Approved) return FlowErrors.KindNotFound;
        await notices.CompletedAsync(flow, ct);
        return Result.Success;
    }

    /// <summary>把指派改成 <paramref name="users"/>；回傳新加入的人與移除的人數。只動 ChangeTracker，與決定同一個交易。</summary>
    private async Task<(IReadOnlySet<Guid> Added, int Removed)> AssignAsync(Guid instanceId, IReadOnlySet<Guid> users, CancellationToken ct)
    {
        var existing = await db.FlowAssignments.Where(x => x.InstanceId == instanceId).ToListAsync(ct);
        var removed = existing.Where(x => !users.Contains(x.UserId)).ToList();
        db.FlowAssignments.RemoveRange(removed);
        var added = users.Except(existing.Select(x => x.UserId)).ToHashSet();
        db.FlowAssignments.AddRange(added.Select(user => new FlowAssignment { InstanceId = instanceId, UserId = user }));
        return (added, removed.Count);
    }

    private async Task<IReadOnlySet<Guid>> EligibleAsync(IReadOnlyCollection<Guid> candidates, Guid applicant, CancellationToken ct)
    {
        var ids = candidates.Where(x => x != applicant).Distinct().ToArray();
        return ids.Length == 0 ? Nobody : (await activeUsers.Ids.Where(x => ids.Contains(x)).ToListAsync(ct)).ToHashSet();
    }

    private static bool Applies(FlowDefinitionStep step, IReadOnlyDictionary<string, string> facts)
        => step.ConditionFact is not { } fact || (facts.TryGetValue(fact, out var value) && value == step.ConditionValue);

    [LoggerMessage(EventId = 3600, EventName = "flow.rule_missing", Level = LogLevel.Warning, Message = "Flow instance {ResourceId} uses approver rule {Code}, which is not registered; the step falls back to flow administrators.")]
    private static partial void LogRuleMissing(ILogger logger, Guid resourceId, string code);

    [LoggerMessage(EventId = 3601, EventName = "flow.no_approver", Level = LogLevel.Warning, Message = "Flow instance {ResourceId} step {Stage} has no eligible approver, including flow administrators.")]
    private static partial void LogNobody(ILogger logger, Guid resourceId, int stage);
}
