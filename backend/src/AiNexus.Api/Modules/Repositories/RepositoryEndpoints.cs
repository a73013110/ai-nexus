using AiNexus.Modules.Identity;
using AiNexus.Modules.Knowledge;

namespace AiNexus.Modules.Repositories;

public static class RepositoryEndpoints
{
    public static void MapRepositories(this RouteGroupBuilder root)
    {
        var api = root.MapGroup("/repositories").RequireAuthorization("feature:repositories").WithTags("Repositories");
        api.MapGet("/commits", async (string repository, CurrentUser current, RepositoryService service, CancellationToken ct) => await service.CommitsAsync((await current.GetAsync(ct)).Id, repository, ct)).WithName("ListRepositoryCommits").Produces<IReadOnlyList<RepositoryCommitDto>>();
        api.MapGet("/reviews", async (string? repository, CurrentUser current, RepositoryReviewService service, CancellationToken ct) => await service.ListAsync((await current.GetAsync(ct)).Id, repository, ct)).WithName("ListRepositoryReviews").Produces<IReadOnlyList<RepositoryReviewDto>>();
        api.MapPost("/reviews", async (CreateRepositoryReviewRequest body, CurrentUser current, RepositoryReviewService service, CancellationToken ct) => await service.CreateAsync((await current.GetAsync(ct)).Id, body, ct)).WithName("CreateRepositoryReview").Produces<RepositoryReviewDto>();
        api.MapGet("/reviews/{id:guid}", async (Guid id, CurrentUser current, RepositoryReviewService service, CancellationToken ct) => await service.DetailAsync((await current.GetAsync(ct)).Id, id, ct)).WithName("ReadRepositoryReview").Produces<RepositoryReviewDetailDto>();
        api.MapPost("/reviews/{id:guid}/cancel", async (Guid id, CurrentUser current, RepositoryReviewService service, CancellationToken ct) => await service.ChangeJobAsync((await current.GetAsync(ct)).Id, id, false, ct)).WithName("CancelRepositoryReview").Produces<AiNexus.Modules.Operations.JobDto>();
        api.MapPost("/reviews/{id:guid}/retry", async (Guid id, CurrentUser current, RepositoryReviewService service, CancellationToken ct) => await service.ChangeJobAsync((await current.GetAsync(ct)).Id, id, true, ct)).WithName("RetryRepositoryReview").Produces<AiNexus.Modules.Operations.JobDto>();
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
