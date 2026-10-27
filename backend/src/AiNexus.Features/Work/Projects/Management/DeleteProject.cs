using AiNexus.Data.App.Projects;
using AiNexus.Features.Work.Collaboration;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Work.Projects.Management;

/// <summary>僅限擁有者的軟刪除，走共用資源的生命週期；對話、檔案與成果會被解除關聯，而不是被銷毀。</summary>
internal sealed class DeleteProject(ResourceLifecycle lifecycle)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapDelete("/{id:guid}", (Guid id, ICurrentUser user, DeleteProject handler, CancellationToken ct) => handler.HandleAsync(user.Id, id, ct).ToHttpResultAsync()).Audited(ProjectsAudit.ProjectDeleted);

    public Task<Result> HandleAsync(Guid actor, Guid id, CancellationToken ct) => lifecycle.DeleteAsync(actor, id, Project.Kind, ct);
}
