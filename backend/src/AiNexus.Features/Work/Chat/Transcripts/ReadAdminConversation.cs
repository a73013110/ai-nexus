using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Work.Conversations;
using AiNexus.Features.Work.Conversations.Administration;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Data;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Chat.Transcripts;

/// <summary>
/// 管理員稽核檢視的對話內容。訊息與擁有者開啟時相同（每個分支與版本都在），
/// 前端以 <see cref="ActiveLeafId"/> 先顯示擁有者最後停留的分支，再切換其他版本。
/// </summary>
public sealed record AdminConversationDetailDto(AdminConversationDto Conversation, string OwnerAccount, string OwnerName, string SystemInstruction, Guid? ActiveLeafId, IReadOnlyList<MessageDto> Messages);

/// <summary>任一使用者的單一對話（含已軟刪除），讀取會稽核；稽核只記則數，不記內容。</summary>
internal sealed class ReadAdminConversation(AppDbContext db, AccessService access, AuditTrail audit, TranscriptReader transcripts)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("/conversations/{id:guid}", async (Guid id, ICurrentUser user, ReadAdminConversation handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, id, ct)).ToHttpResult())
        .Audited(ConversationsAudit.ConversationContentRead)
        .WithName("ReadAdminConversation");

    public async Task<Result<AdminConversationDetailDto>> HandleAsync(Guid actor, Guid id, CancellationToken ct)
    {
        if (!await access.HoldsAsync(actor, FeatureCatalog.IdentityUsers, ct)) return AccessControlErrors.FeatureForbidden;
        var value = await db.Conversations.IgnoreQueryFilters([SoftDelete.Filter]).AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (value is null) return ConversationsErrors.AdminResourceNotFound;
        var owner = await db.Users.AsNoTracking().SingleAsync(x => x.Id == value.OwnerId, ct);
        var messages = await transcripts.ReadAsync(value, ct);
        audit.Describe(subject: owner.Id, details: new { count = messages.Count });
        var conversation = new AdminConversationDto(id, value.Title, value.CreatedAt, value.UpdatedAt, value.IsArchived, value.IsDeleted, messages.Count);
        return new AdminConversationDetailDto(conversation, owner.AdAccount ?? owner.LocalAccount ?? "", owner.DisplayName, value.SystemInstruction, value.ActiveLeafId, messages);
    }
}
