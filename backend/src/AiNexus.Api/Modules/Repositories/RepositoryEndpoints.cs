using AiNexus.Modules.Identity;
using AiNexus.Modules.Knowledge;

namespace AiNexus.Modules.Repositories;

public static class RepositoryEndpoints
{
    public static void MapRepositories(this RouteGroupBuilder root)
    {
        var api = root.MapGroup("/repositories").RequireAuthorization("feature:repositories").WithTags("Repositories");
        api.MapGet("/connection", async (CurrentUser current, RepositoryService service, CancellationToken ct) => Results.Ok(await service.StatusAsync((await current.GetAsync(ct)).Id, ct))).WithName("GetRepositoryConnection").Produces<RepositoryStatusDto>();
        api.MapPost("/connection", async (ConnectRepositoryRequest body, CurrentUser current, RepositoryService service, CancellationToken ct) => Results.Ok(await service.ConnectAsync((await current.GetAsync(ct)).Id, body.Token, ct))).WithName("ConnectRepository").Produces<RepositoryStatusDto>();
        api.MapDelete("/connection", async (CurrentUser current, RepositoryService service, CancellationToken ct) => { await service.DisconnectAsync((await current.GetAsync(ct)).Id, ct); return Results.NoContent(); }).WithName("DisconnectRepository");
        api.MapGet("", async (int? page, CurrentUser current, RepositoryService service, CancellationToken ct) => Results.Ok(await service.ListAsync((await current.GetAsync(ct)).Id, page ?? 1, ct))).WithName("ListRepositories").Produces<RepositoryPageDto>();
        api.MapGet("/tree", async (string repository, string? commit, string? path, CurrentUser current, RepositoryService service, CancellationToken ct) => Results.Ok(await service.TreeAsync((await current.GetAsync(ct)).Id, repository, commit, path, ct))).WithName("ReadRepositoryTree").Produces<RepositoryTreeDto>();
        api.MapGet("/file", async (string repository, string commit, string path, CurrentUser current, RepositoryService service, CancellationToken ct) => Results.Ok(await service.FileAsync((await current.GetAsync(ct)).Id, repository, commit, path, ct))).WithName("ReadRepositoryFile").Produces<RepositoryFileDto>();
        api.MapGet("/issues", async (string repository, CurrentUser current, RepositoryService service, CancellationToken ct) => Results.Ok(await service.IssuesAsync((await current.GetAsync(ct)).Id, repository, ct))).WithName("ListRepositoryIssues").Produces<IReadOnlyList<RepositoryIssueDto>>();
        api.MapPost("/import", async (RepositoryImportRequest body, CurrentUser current, RepositoryService service, CancellationToken ct) => Results.Ok(await service.ImportAsync((await current.GetAsync(ct)).Id, body, ct)))
            .RequireAuthorization("feature:knowledge").WithName("ImportRepositoryFile").Produces<DocumentDto>();
    }
}
