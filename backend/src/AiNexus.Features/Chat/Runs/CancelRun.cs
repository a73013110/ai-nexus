using AiNexus.Features.Identity;
using AiNexus.Features.Inference;
using AiNexus.Features.Inference.Models;
using AiNexus.Features.Inference.Runs;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Chat.Runs;

/// <summary>
/// 中止生成任務端點。
/// 中止排隊中或正在生成中的回覆；若生成已經結束則不變更狀態直接回傳。
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
    /// 【關鍵併發防禦】：
    /// 透過對話鎖（LockConversationAsync）保護，
    /// 防止取消指令與背景 Worker 的 Token 沖刷（Flush）發生競態衝突覆蓋彼此的狀態。
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

            // 已結束的 Run 不再變更狀態（重複按取消或與 Worker 競態時的冪等行為）
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
