using AiNexus.BuildingBlocks;
using AiNexus.Modules.Inference;
using AiNexus.Modules.Operations;
using Microsoft.EntityFrameworkCore;
using AiNexus.Database;
using EDoc.Core.Database.Interfaces;

namespace AiNexus.Modules.Conversations;

public sealed class ConversationService(IEfHelper<INexusDatabase> ef, GenerationScheduler scheduler, ModelPresentation presentation)
{
    public async Task<Conversation> OwnedAsync(Guid owner, Guid id, CancellationToken ct)
        => await ef.Set<Conversation>().SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == owner && !x.IsDeleted, ct)
           ?? throw new ApiException(404, "conversation_not_found", "找不到這個對話。");

    public async Task<IReadOnlyList<ConversationDto>> ListAsync(Guid owner, string? search, int offset, CancellationToken ct)
    {
        if (search?.Length > 120 || offset < 0 || offset > 100000) throw new ApiException(400, "invalid_query", "搜尋條件不正確。");
        var query = ef.Set<Conversation>().AsNoTracking().Where(x => x.OwnerId == owner && !x.IsDeleted);
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.Title.Contains(search.Trim()));
        var rows = await query.OrderByDescending(x => x.UpdatedAt).ThenBy(x => x.Id).Skip(offset).Take(100).ToListAsync(ct);
        return rows.Select(x => x.ToDto()).ToList();
    }

    public async Task<ConversationDto> CreateAsync(Guid owner, string? title, CancellationToken ct)
    {
        var conversation = new Conversation { OwnerId = owner, Title = CleanTitle(title ?? "新對話") };
        ef.Set<Conversation>().Add(conversation);
        ef.Set<AuditEvent>().Add(new AuditEvent { OwnerId = owner, Action = "conversation.created", ResourceId = conversation.Id });
        await ef.SaveChangesAsync(ct);
        return conversation.ToDto();
    }

    public async Task<ConversationDetailDto> DetailAsync(Guid owner, Guid id, CancellationToken ct)
    {
        var conversation = await OwnedAsync(owner, id, ct);
        var messages = await ef.Set<Message>().AsNoTracking().Where(x => x.ConversationId == id).OrderBy(x => x.CreatedAt).ToListAsync(ct);
        var run = await ef.Set<GenerationRun>().AsNoTracking().SingleOrDefaultAsync(x => x.ConversationId == id && x.ActiveOwnerId == owner, ct);
        return new(conversation.ToDto(), messages.Select(presentation.Message).ToList(), run is null ? null : presentation.Run(run));
    }

    public async Task<ConversationDto> RenameAsync(Guid owner, Guid id, string title, CancellationToken ct)
    {
        var conversation = await OwnedAsync(owner, id, ct);
        conversation.Title = CleanTitle(title);
        conversation.UpdatedAt = DateTimeOffset.UtcNow;
        ef.Set<AuditEvent>().Add(new AuditEvent { OwnerId = owner, Action = "conversation.renamed", ResourceId = id });
        await ef.SaveChangesAsync(ct);
        return conversation.ToDto();
    }

    public async Task SelectBranchAsync(Guid owner, Guid id, Guid leaf, CancellationToken ct)
    {
        await scheduler.StateGate.WaitAsync(ct);
        try
        {
            var conversation = await OwnedAsync(owner, id, ct);
            if (await ef.Set<GenerationRun>().AnyAsync(x => x.ConversationId == id && x.ActiveOwnerId != null, ct)) throw new ApiException(409, "generation_active", "請先停止生成，再切換版本。");
            if (!await ef.Set<Message>().AnyAsync(x => x.Id == leaf && x.ConversationId == id, ct)) throw new ApiException(404, "message_not_found", "找不到這個版本。");
            conversation.ActiveLeafId = leaf;
            conversation.UpdatedAt = DateTimeOffset.UtcNow;
            await ef.SaveChangesAsync(ct);
        }
        finally { scheduler.StateGate.Release(); }
    }

    public async Task DeleteAsync(Guid owner, Guid id, CancellationToken ct)
    {
        await scheduler.StateGate.WaitAsync(ct);
        try
        {
            var conversation = await OwnedAsync(owner, id, ct);
            if (await ef.Set<GenerationRun>().AnyAsync(x => x.ConversationId == id && x.ActiveOwnerId != null, ct)) throw new ApiException(409, "generation_active", "請先停止生成，再刪除對話。");
            conversation.IsDeleted = true;
            ef.Set<AuditEvent>().Add(new AuditEvent { OwnerId = owner, Action = "conversation.deleted", ResourceId = id });
            await ef.SaveChangesAsync(ct);
        }
        finally { scheduler.StateGate.Release(); }
    }

    // The inference module changes conversation history through this module's contract.
    public async Task<(Message User, Message Assistant)> PrepareGenerationAsync(Guid owner, CreateRunRequest request, Guid runId, CancellationToken ct)
    {
        var conversation = await OwnedAsync(owner, request.ConversationId, ct);
        Message user;
        if (request.RegenerateUserMessageId is Guid userId)
        {
            if (request.Prompt is not null || request.ParentMessageId is not null) throw new ApiException(400, "invalid_request", "重新生成不可同時提交新提問。");
            user = await ef.Set<Message>().SingleOrDefaultAsync(x => x.Id == userId && x.ConversationId == conversation.Id && x.Role == "user", ct)
                   ?? throw new ApiException(404, "message_not_found", "找不到原始提問。");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.Prompt)) throw new ApiException(400, "prompt_required", "請輸入訊息。");
            if (request.ParentMessageId is Guid parentId && !await ef.Set<Message>().AnyAsync(x => x.Id == parentId && x.ConversationId == conversation.Id && x.Role == "assistant" && x.Status != RunStates.Queued && x.Status != RunStates.Running, ct))
                throw new ApiException(400, "invalid_parent", "上文必須是此對話中已結束的回答。");
            user = new Message { ConversationId = conversation.Id, ParentId = request.ParentMessageId, Content = request.Prompt.Trim() };
            ef.Set<Message>().Add(user);
            if (conversation.ActiveLeafId is null && conversation.Title == "新對話") conversation.Title = string.Concat(user.Content.Replace('\n', ' ').Take(36));
        }
        var assistant = new Message { ConversationId = conversation.Id, ParentId = user.Id, Role = "assistant", Status = RunStates.Queued, RunId = runId, ModelId = request.ModelId };
        ef.Set<Message>().Add(assistant);
        conversation.ActiveLeafId = assistant.Id;
        conversation.UpdatedAt = DateTimeOffset.UtcNow;
        return (user, assistant);
    }

    public async Task UpdateAnswerAsync(GenerationRun run, CancellationToken ct)
    {
        var message = await ef.Set<Message>().SingleAsync(x => x.Id == run.AssistantMessageId, ct);
        message.Content = run.Content;
        message.Status = run.Status;
        var conversation = await ef.Set<Conversation>().SingleAsync(x => x.Id == run.ConversationId, ct);
        conversation.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static string CleanTitle(string title)
    {
        title = title.Trim();
        if (title.Length is < 1 or > 120) throw new ApiException(400, "invalid_title", "標題需為 1 至 120 個字元。");
        return title;
    }
}
