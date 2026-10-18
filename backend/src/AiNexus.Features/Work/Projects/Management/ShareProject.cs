using AiNexus.Data.App.Projects;
using AiNexus.Features.Work.Collaboration;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Work.Projects.Management;

/// <summary>決定誰能讀取或編輯專案，透過共用資源的存取清單設定（僅限擁有者）。</summary>
internal sealed class ShareProject(ResourceAccess access)
{
    public static void Map(RouteGroupBuilder routes)
    {
        routes.MapGet("/{id:guid}/access", (Guid id, ICurrentUser user, ShareProject handler, CancellationToken ct) => handler.GetAsync(user.Id, id, ct).ToHttpResultAsync());
        routes.MapPut("/{id:guid}/access", (Guid id, ResourceAclRequest body, ICurrentUser user, ShareProject handler, CancellationToken ct) => handler.SetAsync(user.Id, id, body, ct).ToHttpResultAsync());
    }

    public Task<Result<ResourceAclDto>> GetAsync(Guid actor, Guid id, CancellationToken ct) => access.AclAsync(actor, id, Project.Kind, ct);

    public Task<Result> SetAsync(Guid actor, Guid id, ResourceAclRequest request, CancellationToken ct) => access.SetAclAsync(actor, id, Project.Kind, request, ct);
}
