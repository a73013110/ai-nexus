using AiNexus.Modules.Identity;
using AiNexus.Modules.Collaboration;
using AiNexus.Modules.AccessControl;
using Microsoft.EntityFrameworkCore;
using AiNexus.BuildingBlocks;

namespace AiNexus.Modules.Knowledge;

public static class KnowledgeEndpoints
{
    public static void MapKnowledge(this RouteGroupBuilder api)
    {
        var routes = api.MapGroup("/knowledge").RequireAuthorization("feature:knowledge").WithTags("Knowledge");
        routes.MapGet("/collections", async (CurrentUser current, DocumentService docs, CancellationToken ct) => await docs.CollectionsAsync((await current.GetAsync(ct)).Id, ct)).WithName("ListKnowledgeCollections").Produces<IReadOnlyList<CollectionDto>>();
        routes.MapPost("/collections", async (CollectionRequest request, CurrentUser current, DocumentService docs, CancellationToken ct) => await docs.CreateCollectionAsync((await current.GetAsync(ct)).Id, request, ct)).WithName("CreateKnowledgeCollection").Produces<CollectionDto>();
        routes.MapPut("/collections/{id:guid}", async (Guid id, CollectionRequest request, CurrentUser current, DocumentService docs, CancellationToken ct) => { await docs.UpdateCollectionAsync((await current.GetAsync(ct)).Id, id, request, ct); return Results.NoContent(); }).WithName("UpdateKnowledgeCollection").Produces(204);
        routes.MapDelete("/collections/{id:guid}", async (Guid id, CurrentUser current, DocumentService docs, CancellationToken ct) => { await docs.DeleteCollectionAsync((await current.GetAsync(ct)).Id, id, ct); return Results.NoContent(); }).WithName("DeleteKnowledgeCollection").Produces(204);
        routes.MapGet("/collections/{id:guid}/documents", async (Guid id, CurrentUser current, DocumentService docs, CancellationToken ct) => await docs.ListAsync((await current.GetAsync(ct)).Id, id, ct)).WithName("ListKnowledgeDocuments").Produces<IReadOnlyList<DocumentDto>>();
        routes.MapPost("/collections/{id:guid}/documents", async (Guid id, AddDocumentRequest request, CurrentUser current, DocumentService docs, CancellationToken ct) => await docs.AddAsync((await current.GetAsync(ct)).Id, id, request.AttachmentId, ct)).WithName("AddKnowledgeDocument").Produces<DocumentDto>();
        routes.MapGet("/collections/{id:guid}/access", async (Guid id, CurrentUser current, ResourceAccess access, CancellationToken ct) => await access.AclAsync((await current.GetAsync(ct)).Id, id, "knowledge", ct)).WithName("GetKnowledgeAccess").Produces<ResourceAclDto>();
        routes.MapPut("/collections/{id:guid}/access", async (Guid id, ResourceAclRequest request, CurrentUser current, ResourceAccess access, CancellationToken ct) => { await access.SetAclAsync((await current.GetAsync(ct)).Id, id, "knowledge", request, ct); return Results.NoContent(); }).WithName("SaveKnowledgeAccess").Produces(204);
        routes.MapPost("/search", async (KnowledgeSearchRequest request, CurrentUser current, KnowledgeRetrieval search, CancellationToken ct) => await search.SearchAsync((await current.GetAsync(ct)).Id, request, ct)).WithName("SearchKnowledge").Produces<KnowledgeSearchDto>();
        var selection = api.MapGroup("/conversations/{id:guid}/knowledge").RequireAuthorization("feature:knowledge").RequireAuthorization(BuiltInAccess.ChatPolicy).WithTags("Knowledge");
        selection.MapGet("", async (Guid id, CurrentUser current, KnowledgeRetrieval search, CancellationToken ct) => await search.SelectionAsync((await current.GetAsync(ct)).Id, id, ct)).WithName("ConversationKnowledge").Produces<KnowledgeSelectionDto>();
        selection.MapPut("", async (Guid id, KnowledgeSelectionDto request, CurrentUser current, KnowledgeRetrieval search, CancellationToken ct) => { await search.SetSelectionAsync((await current.GetAsync(ct)).Id, id, request, ct); return Results.NoContent(); }).WithName("SaveConversationKnowledge").Produces(204);
        var documents = api.MapGroup("/documents").WithTags("Documents");
        documents.MapGet("/{id:guid}", async (Guid id, CurrentUser current, DocumentService docs, CancellationToken ct) => await docs.DetailAsync((await current.GetAsync(ct)).Id, id, ct)).WithName("GetDocument").Produces<DocumentDto>();
        documents.MapGet("/{id:guid}/pages", async (Guid id, CurrentUser current, DocumentService docs, CancellationToken ct) => await docs.PagesAsync((await current.GetAsync(ct)).Id, id, ct)).WithName("DocumentPages").Produces<IReadOnlyList<DocumentPageDto>>();
        documents.MapGet("/{id:guid}/job", async (Guid id, CurrentUser current, DocumentService docs, CancellationToken ct) => await docs.JobAsync((await current.GetAsync(ct)).Id, id, ct)).WithName("DocumentJob").Produces<DocumentJobDto>();
        documents.MapGet("/{id:guid}/content", async (Guid id, bool? download, HttpContext http, CurrentUser current, DocumentService docs, AiNexus.Modules.Attachments.AttachmentService files, CancellationToken ct) =>
        {
            var file = await docs.OriginalAsync((await current.GetAsync(ct)).Id, id, ct);
            return WebSecurity.File(http, await files.OpenAsync(file, ct), file.ContentType, file.FileName, download == true);
        }).WithName("DocumentOriginal");
        documents.MapDelete("/{id:guid}", async (Guid id, CurrentUser current, DocumentService docs, CancellationToken ct) => { await docs.DeleteAsync((await current.GetAsync(ct)).Id, id, ct); return Results.NoContent(); }).WithName("DeleteDocument").Produces(204);
        documents.MapPost("/{id:guid}/reindex", async (Guid id, CurrentUser current, DocumentService docs, CancellationToken ct) => await docs.ReindexAsync((await current.GetAsync(ct)).Id, id, ct)).WithName("ReindexDocument").Produces<DocumentDto>();
        api.MapPost("/attachments/{id:guid}/document", async (Guid id, CurrentUser current, DocumentService docs, CancellationToken ct) => await docs.AddAsync((await current.GetAsync(ct)).Id, null, id, ct)).RequireAuthorization("feature:attachments").WithName("ReadAttachmentDocument").Produces<DocumentDto>();
        api.MapGet("/directory", async (string search, CurrentUser current, NexusDbContext db, CancellationToken ct) =>
        {
            await current.GetAsync(ct);
            if (search.Trim().Length is < 2 or > 120) return Results.Ok(Array.Empty<DirectoryUserDto>());
            return Results.Ok(await db.Users.AsNoTracking().Where(x => x.Account.Contains(search.Trim()) || x.DisplayName.Contains(search.Trim())).OrderBy(x => x.Account).Take(20).Select(x => new DirectoryUserDto(x.Id, x.Account, x.DisplayName)).ToListAsync(ct));
        }).WithName("SearchUserDirectory").Produces<IReadOnlyList<DirectoryUserDto>>();
        api.MapGet("/directory/groups", async (CurrentUser current, NexusDbContext db, CancellationToken ct) => { await current.GetAsync(ct); return await db.Set<RoleGroup>().AsNoTracking().Where(x => x.Enabled).OrderBy(x => x.Name).Select(x => new DirectoryGroupDto(x.Id, x.Name)).ToListAsync(ct); }).WithName("ListDirectoryGroups").Produces<IReadOnlyList<DirectoryGroupDto>>();
    }
}
