using AiNexus.Data.App.Inference;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Platform.Inference;
using AiNexus.Features.Platform.Inference.Models;
using AiNexus.Features.Platform.Inference.Runs;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Work.Chat.Runs;

/// <summary>
/// 中止生成任務的端點。中止排隊中或生成中的回覆；生成已結束時不變更狀態，直接回傳目前結果。
/// </summary>
internal sealed class CancelRun(
    GenerationScheduler scheduler,
    RunService runs,
    ModelPresentation presentation)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPost("/runs/{id:guid}/cancel", async (Guid id, ICurrentUser user, CancelRun handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, id, ct)).ToHttpResult())
        .WithName("CancelRun");

    /// <summary>
    /// 在對話鎖（LockConversationAsync）內結算取消，與背景 Worker 的增量寫入（flush）互斥，
    /// 否則兩邊會互相覆蓋狀態。
    /// </summary>
    public async Task<Result<RunDto>> HandleAsync(Guid owner, Guid id, CancellationToken ct)
    {
        if (await runs.ConversationOfAsync(owner, id, ct) is not Guid conversation)
        {
            return InferenceErrors.RunNotFound;
        }

        using (await scheduler.LockConversationAsync(conversation, ct))
        {
            var found = await runs.OwnedAsync(owner, id, ct);
            if (!found.IsSuccess) return found.Error;
            var run = found.Value;

            // 已結束的 Run 不再變更狀態；重複按取消，或與 Worker 同時結算時，結果都會一致
            if (!RunStates.IsActive(run.Status))
            {
                return presentation.Run(run);
            }

            scheduler.Cancel(id);
            await runs.FinishAsync(run, RunStates.Cancelled, null, ct);

            return presentation.Run(run);
        }
    }
}
