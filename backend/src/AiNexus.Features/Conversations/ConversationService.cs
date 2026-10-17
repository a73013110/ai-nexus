using AiNexus.Features.Inference.Runs;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Data;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Conversations;

/// <summary>
/// 使用者對話互動回合請求資料傳輸物件。
/// </summary>
/// <remarks>
/// 兩種模式：
/// 1. 新問題模式：提供 <paramref name="Prompt"/> 與父訊息 <paramref name="ParentMessageId"/>，在對話分支樹下方掛載新提問。
/// 2. 重新回答模式：指定 <paramref name="RegenerateUserMessageId"/>，針對該歷史提問重新生成全新 Assistant 答案分支（此時禁止傳入 Prompt）。
/// </remarks>
public sealed record ConversationTurn(
    Guid ConversationId,
    string? Prompt,
    Guid? ParentMessageId,
    Guid? RegenerateUserMessageId);

/// <summary>
/// 跨模組對話服務契約。
/// 提供給 Chat、Inference、Knowledge、Artifacts 模組操作對話歷史與訊息樹。
/// </summary>
public sealed class ConversationService(NexusDbContext db, TimeProvider clock)
{
    /// <summary>
    /// 取得使用者擁有的對話實體（受 EF 跟蹤，不預先載入標籤）。供後續需修改對話屬性之操作使用。
    /// </summary>
    public async Task<Result<Conversation>> OwnedAsync(Guid owner, Guid id, CancellationToken ct)
        => await db.Set<Conversation>().SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == owner, ct) is { } conversation
            ? conversation
            : ConversationsErrors.NotFound;

    /// <summary>
    /// 快速驗證對話是否存在且屬於該使用者（僅使用 SQL ANY，不將實體載入記憶體）。
    /// </summary>
    public async Task<Result> RequireOwnedAsync(Guid owner, Guid id, CancellationToken ct)
        => await db.Set<Conversation>().AnyAsync(x => x.Id == id && x.OwnerId == owner, ct)
            ? Result.Success
            : ConversationsErrors.NotFound;

    public Task<Result<ConversationDto>> CreateAsync(Guid owner, string? title, CancellationToken ct)
        => new CreateConversation(db, clock).HandleAsync(owner, title, ct);

    /// <summary>
    /// 準備模型生成作業：在資料庫中登記 User 與 Assistant 訊息骨架。
    /// </summary>
    /// <remarks>
    /// 狀態檢查與樹狀結構維護：
    /// 1. 對話狀態：若對話已被封存（<see cref="Conversation.IsArchived"/>），禁止繼續提問。
    /// 2. 重新回答分支防禦：若為重新回答模式，確認原始提問存在且為 user 角色；若帶有新 Prompt 則視為不合法請求拒絕。
    /// 3. 新問題分支防禦：檢查 ParentId 指向的訊息必須為已完成生成之 assistant 訊息（非排隊中或運行中）。
    /// 4. 首次提問自動標題：若對話尚無葉節點且標題為預設「新對話」，自動擷取首次提問前 36 個字元作為對話標題。
    /// 5. 更新作用中葉節點：將對話的 <see cref="Conversation.ActiveLeafId"/> 指向新建立的 assistant 訊息。
    /// </remarks>
    public async Task<Result<(Message User, Message Assistant)>> PrepareGenerationAsync(
        Conversation conversation,
        ConversationTurn request,
        string modelId,
        Guid runId,
        CancellationToken ct)
    {
        if (conversation.Id != request.ConversationId)
        {
            throw new ArgumentException("The turn belongs to another conversation.", nameof(request));
        }

        if (conversation.IsArchived)
        {
            return ConversationsErrors.Archived;
        }

        Message user;
        if (request.RegenerateUserMessageId is Guid userId)
        {
            if (request.Prompt is not null || request.ParentMessageId is not null)
            {
                return ConversationsErrors.RegenerateWithPrompt;
            }

            var original = await db.Set<Message>()
                .SingleOrDefaultAsync(x => x.Id == userId && x.ConversationId == conversation.Id && x.Role == "user", ct);

            if (original is null)
            {
                return ConversationsErrors.MessageNotFound;
            }

            user = original;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.Prompt))
            {
                return ConversationsErrors.PromptRequired;
            }

            if (request.ParentMessageId is Guid parentId && !await db.Set<Message>().AnyAsync(
                x => x.Id == parentId && x.ConversationId == conversation.Id && x.Role == "assistant" &&
                     x.Status != RunStates.Queued && x.Status != RunStates.Running, ct))
            {
                return ConversationsErrors.InvalidParent;
            }

            user = new Message
            {
                ConversationId = conversation.Id,
                ParentId = request.ParentMessageId,
                Content = request.Prompt.Trim()
            };

            db.Set<Message>().Add(user);

            if (conversation.ActiveLeafId is null && conversation.Title == "新對話")
            {
                conversation.Title = string.Concat(user.Content.Replace('\n', ' ').Take(36));
            }
        }

        var assistant = new Message
        {
            ConversationId = conversation.Id,
            ParentId = user.Id,
            Role = "assistant",
            Status = RunStates.Queued,
            RunId = runId,
            ModelId = modelId
        };

        db.Set<Message>().Add(assistant);

        conversation.ActiveLeafId = assistant.Id;
        conversation.UpdatedAt = clock.GetUtcNow();

        return (user, assistant);
    }

    /// <summary>
    /// 模型推論完成或終止時，更新 Assistant 訊息之最終文字、狀態與錯誤/問題代碼。
    /// </summary>
    public async Task UpdateAnswerAsync(GenerationRun run, CancellationToken ct)
    {
        var message = await db.Set<Message>().SingleAsync(x => x.Id == run.AssistantMessageId, ct);
        message.Content = run.Content;
        message.Status = run.Status;
        message.ErrorCode = run.ErrorCode;
        message.IssueCode = run.IssueCode;

        // 對話若在生成途中被軟刪除，仍要更新 UpdatedAt，所以必須略過軟刪除過濾器，否則查不到該列。
        var conversation = await db.Set<Conversation>()
            .IgnoreQueryFilters([SoftDelete.Filter])
            .SingleAsync(x => x.Id == run.ConversationId, ct);

        conversation.UpdatedAt = clock.GetUtcNow();
    }

    /// <summary>
    /// 串流生成的增量追加：把 delta 接在既有回答內容後面。
    /// </summary>
    /// <remarks>
    /// 在 SQL Server 端執行 <c>Content + delta</c>，不把既有長文本載入記憶體再回寫，寫入量只與增量長度有關。
    /// 本方法不檢查 Assistant 狀態，呼叫端必須先確認答案仍在生成中；應於呼叫端的資料庫交易中執行。
    /// </remarks>
    public async Task AppendAnswerAsync(Guid answer, Guid conversation, string delta, CancellationToken ct)
    {
        if (delta.Length > 0)
        {
            await db.Set<Message>()
                .Where(x => x.Id == answer)
                .ExecuteUpdateAsync(p => p.SetProperty(x => x.Content, x => x.Content + delta), ct);
        }

        var now = clock.GetUtcNow();
        await db.Set<Conversation>()
            .IgnoreQueryFilters([SoftDelete.Filter])
            .Where(x => x.Id == conversation)
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.UpdatedAt, now), ct);
    }
}
