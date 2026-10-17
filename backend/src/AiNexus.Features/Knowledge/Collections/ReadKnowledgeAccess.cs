using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Knowledge.Collections;

/// <summary>擁有者讀取可檢視或編輯此知識庫的成員與群組授權。</summary>
internal sealed class ReadKnowledgeAccess(ResourceAccess access)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/collections/{id:guid}/access", (Guid id, ICurrentUser user, ReadKnowledgeAccess handler, CancellationToken ct) => handler.HandleAsync(user.Id, id, ct).ToHttpResultAsync())
        .WithName("GetKnowledgeAccess");

    public Task<Result<ResourceAclDto>> HandleAsync(Guid actor, Guid id, CancellationToken ct) => access.AclAsync(actor, id, KnowledgeCollection.Kind, ct);
}
