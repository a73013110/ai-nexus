using AiNexus.Platform.Errors;
using AiNexus.Features.Persistence;
using AiNexus.Features.Inference;
using AiNexus.Platform.Data;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Conversations;

/// <summary>A question for the model: a new prompt below <paramref name="ParentMessageId"/>, or another answer to an earlier question.</summary>
public sealed record ConversationTurn(Guid ConversationId, string? Prompt, Guid? ParentMessageId, Guid? RegenerateUserMessageId);

/// <summary>
/// The module's contract for other modules (generation, knowledge, artifacts, source chats). Failures are thrown as
/// <see cref="ApiException"/> for callers that cannot return a result; the module's own endpoints are the slices next to this file.
/// </summary>
public sealed class ConversationService(NexusDbContext db, TimeProvider clock)
{
    public async Task<Conversation> OwnedAsync(Guid owner, Guid id, CancellationToken ct)
        => await db.OwnedConversationAsync(owner, id, ct)
           ?? throw new ApiException(404, "conversation_not_found", "找不到這個對話。");

    public async Task<ConversationDto> CreateAsync(Guid owner, string? title, CancellationToken ct)
    {
        var created = await CreateConversation.HandleAsync(db, clock, owner, title, ct);
        return created.IsSuccess ? created.Value : throw created.Error.ToException();
    }

    // The chat module changes conversation history through this module's contract.
    public async Task<(Message User, Message Assistant)> PrepareGenerationAsync(Guid owner, ConversationTurn request, string modelId, Guid runId, CancellationToken ct)
    {
        var conversation = await OwnedAsync(owner, request.ConversationId, ct);
        if (conversation.IsArchived) throw new ApiException(409, "conversation_archived", "請先還原封存對話，再繼續提問。");
        Message user;
        if (request.RegenerateUserMessageId is Guid userId)
        {
            if (request.Prompt is not null || request.ParentMessageId is not null) throw new ApiException(400, "invalid_request", "重新生成不可同時提交新提問。");
            user = await db.Set<Message>().SingleOrDefaultAsync(x => x.Id == userId && x.ConversationId == conversation.Id && x.Role == "user", ct)
                   ?? throw new ApiException(404, "message_not_found", "找不到原始提問。");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.Prompt)) throw new ApiException(400, "prompt_required", "請輸入訊息。");
            if (request.ParentMessageId is Guid parentId && !await db.Set<Message>().AnyAsync(x => x.Id == parentId && x.ConversationId == conversation.Id && x.Role == "assistant" && x.Status != RunStates.Queued && x.Status != RunStates.Running, ct))
                throw new ApiException(400, "invalid_parent", "上文必須是此對話中已結束的回答。");
            user = new Message { ConversationId = conversation.Id, ParentId = request.ParentMessageId, Content = request.Prompt.Trim() };
            db.Set<Message>().Add(user);
            if (conversation.ActiveLeafId is null && conversation.Title == "新對話") conversation.Title = string.Concat(user.Content.Replace('\n', ' ').Take(36));
        }
        var assistant = new Message { ConversationId = conversation.Id, ParentId = user.Id, Role = "assistant", Status = RunStates.Queued, RunId = runId, ModelId = modelId };
        db.Set<Message>().Add(assistant);
        conversation.ActiveLeafId = assistant.Id;
        conversation.UpdatedAt = clock.GetUtcNow();
        return (user, assistant);
    }

    public async Task UpdateAnswerAsync(GenerationRun run, CancellationToken ct)
    {
        var message = await db.Set<Message>().SingleAsync(x => x.Id == run.AssistantMessageId, ct);
        message.Content = run.Content;
        message.Status = run.Status;
        message.ErrorCode = run.ErrorCode; message.IssueCode = run.IssueCode;
        var conversation = await db.Set<Conversation>().IgnoreQueryFilters([SoftDelete.Filter]).SingleAsync(x => x.Id == run.ConversationId, ct);
        conversation.UpdatedAt = clock.GetUtcNow();
    }

    /// <summary>
    /// Streaming form of <see cref="UpdateAnswerAsync"/> while the answer's status does not change: appends the delta in SQL
    /// (the stored text is neither read nor resent) and touches the conversation. Runs immediately, so call it inside the
    /// caller's transaction.
    /// </summary>
    public async Task AppendAnswerAsync(Guid answer, Guid conversation, string delta, CancellationToken ct)
    {
        if (delta.Length > 0)
            await db.Set<Message>().Where(x => x.Id == answer).ExecuteUpdateAsync(p => p.SetProperty(x => x.Content, x => x.Content + delta), ct);
        var now = clock.GetUtcNow();
        await db.Set<Conversation>().IgnoreQueryFilters([SoftDelete.Filter]).Where(x => x.Id == conversation).ExecuteUpdateAsync(p => p.SetProperty(x => x.UpdatedAt, now), ct);
    }
}
