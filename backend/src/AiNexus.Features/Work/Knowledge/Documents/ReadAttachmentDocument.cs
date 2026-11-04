using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Work.Knowledge.Documents;

/// <summary>把附件讀成獨立文件，供對話中的文件檢視器使用；加入流程與知識庫相同（<see cref="AddKnowledgeDocument"/>），只是授權看附件。</summary>
internal sealed class ReadAttachmentDocument : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapPost("/{id:guid}/document", (Guid id, ICurrentUser user, AddKnowledgeDocument handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, null, id, ct))
        .Audited(KnowledgeAudit.DocumentCreated)
        .WithName("ReadAttachmentDocument");
}
