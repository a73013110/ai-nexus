using AiNexus.Data.App.Knowledge;
using AiNexus.Features.Work.Collaboration;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Work.Knowledge.Collections;

/// <summary>擁有者整批取代知識庫的指定成員與群組授權。</summary>
internal sealed class SaveKnowledgeAccess(ResourceAccess access)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPut("/collections/{id:guid}/access", (Guid id, ResourceAclRequest request, ICurrentUser user, SaveKnowledgeAccess handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, id, request, ct).ToHttpResultAsync())
        .WithName("SaveKnowledgeAccess");

    public Task<Result> HandleAsync(Guid actor, Guid id, ResourceAclRequest request, CancellationToken ct) => access.SetAclAsync(actor, id, Collection.Kind, request, ct);
}
