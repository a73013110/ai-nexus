using AiNexus.Features.Conversations;
using AiNexus.Features.Identity;
using AiNexus.Features.Inference;
using AiNexus.Features.Artifacts;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Knowledge;

namespace AiNexus.Features.Projects;

public static class ProjectEndpoints
{
    public static void MapProjects(this RouteGroupBuilder api)
    {
        api.MapPut("/conversations/{id:guid}/project", async (Guid id, ConversationProjectRequest body, CurrentUser u, ProjectService s, AiNexus.Features.Inference.GenerationScheduler scheduler, CancellationToken ct) => Results.Ok(await s.AssignConversationAsync((await u.GetAsync(ct)).Id, id, body.ProjectId, scheduler, ct))).RequireAuthorization(AiNexus.Features.AccessControl.BuiltInAccess.ChatPolicy).Produces<ConversationDto>();
        var routes = api.MapGroup("/projects").RequireAuthorization("feature:projects").WithTags("Projects");
        routes.MapGet("", async (CurrentUser u, ProjectService s, CancellationToken ct) => Results.Ok(await s.ListAsync((await u.GetAsync(ct)).Id, ct))).Produces<IReadOnlyList<ProjectDto>>();
        routes.MapPost("", async (ProjectRequest body, CurrentUser u, ProjectService s, CancellationToken ct) => Results.Ok(await s.CreateAsync((await u.GetAsync(ct)).Id, body, ct))).Produces<ProjectDto>();
        routes.MapDelete("/{id:guid}", async (Guid id, CurrentUser u, ResourceLifecycle s, CancellationToken ct) => { await s.DeleteAsync((await u.GetAsync(ct)).Id, id, "project", ct); return Results.NoContent(); });
        routes.MapGet("/{id:guid}", async (Guid id, CurrentUser u, ProjectService s, CancellationToken ct) => Results.Ok(await s.GetAsync((await u.GetAsync(ct)).Id, id, ct))).Produces<ProjectDto>();
        routes.MapPut("/{id:guid}", async (Guid id, ProjectRequest body, CurrentUser u, ProjectService s, CancellationToken ct) => Results.Ok(await s.SaveAsync((await u.GetAsync(ct)).Id, id, body, ct))).Produces<ProjectDto>();
        routes.MapGet("/{id:guid}/access", async (Guid id, CurrentUser u, ResourceAccess s, CancellationToken ct) => Results.Ok(await s.AclAsync((await u.GetAsync(ct)).Id, id, "project", ct))).Produces<ResourceAclDto>();
        routes.MapPut("/{id:guid}/access", async (Guid id, ResourceAclRequest body, CurrentUser u, ResourceAccess s, CancellationToken ct) => { await s.SetAclAsync((await u.GetAsync(ct)).Id, id, "project", body, ct); return Results.NoContent(); });
        routes.MapGet("/{id:guid}/files", async (Guid id, CurrentUser u, ProjectService s, CancellationToken ct) => Results.Ok(await s.FilesAsync((await u.GetAsync(ct)).Id, id, ct))).Produces<IReadOnlyList<DocumentDto>>();
        routes.MapPost("/{id:guid}/files", async (Guid id, AddDocumentRequest body, CurrentUser u, ProjectService s, CancellationToken ct) => Results.Ok(await s.AddFileAsync((await u.GetAsync(ct)).Id, id, body.AttachmentId, ct))).Produces<DocumentDto>();
        routes.MapGet("/{id:guid}/templates", async (Guid id, CurrentUser u, ProjectService s, CancellationToken ct) => Results.Ok(await s.TemplatesAsync((await u.GetAsync(ct)).Id, id, ct))).Produces<IReadOnlyList<ProjectTemplateDto>>();
        routes.MapPost("/{id:guid}/templates", async (Guid id, ProjectTemplateRequest body, CurrentUser u, ProjectService s, CancellationToken ct) => Results.Ok(await s.SaveTemplateAsync((await u.GetAsync(ct)).Id, id, null, body, ct))).Produces<ProjectTemplateDto>();
        routes.MapPut("/{id:guid}/templates/{key:guid}", async (Guid id, Guid key, ProjectTemplateRequest body, CurrentUser u, ProjectService s, CancellationToken ct) => Results.Ok(await s.SaveTemplateAsync((await u.GetAsync(ct)).Id, id, key, body, ct))).Produces<ProjectTemplateDto>();
        routes.MapDelete("/{id:guid}/templates/{key:guid}", async (Guid id, Guid key, CurrentUser u, ProjectService s, CancellationToken ct) => { await s.DeleteTemplateAsync((await u.GetAsync(ct)).Id, id, key, ct); return Results.NoContent(); });
        routes.MapGet("/{id:guid}/conversations", async (Guid id, CurrentUser u, ProjectService s, CancellationToken ct) => Results.Ok(await s.ConversationsAsync((await u.GetAsync(ct)).Id, id, ct))).Produces<IReadOnlyList<ConversationDto>>();
        routes.MapPost("/{id:guid}/conversations", async (Guid id, ProjectConversationRequest body, CurrentUser u, ProjectService s, CancellationToken ct) => Results.Ok(await s.StartAsync((await u.GetAsync(ct)).Id, id, body, ct))).Produces<ProjectConversationDto>();
        routes.MapPost("/{id:guid}/artifacts", async (Guid id, CreateArtifactRequest body, CurrentUser u, ArtifactService s, CancellationToken ct) => Results.Ok(await s.CreateAsync((await u.GetAsync(ct)).Id, body with { ProjectId = id }, ct))).RequireAuthorization("feature:artifacts").Produces<ArtifactDto>();
    }
}
