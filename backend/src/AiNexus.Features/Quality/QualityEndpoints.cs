using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;

namespace AiNexus.Features.Quality;
public static class QualityEndpoints
{
    public static void MapQuality(this RouteGroupBuilder api)
    {
        api.MapGet("/messages/{id:guid}/feedback", async (Guid id, CurrentUser u, QualityService s, CancellationToken ct) => Results.Ok(await s.FeedbackForAsync((await u.GetAsync(ct)).Id, id, ct))).RequireAuthorization("feature:chat").WithTags("Quality").Produces<FeedbackDto>();
        api.MapPut("/messages/{id:guid}/feedback", async (Guid id, FeedbackRequest body, CurrentUser u, QualityService s, CancellationToken ct) => Results.Ok(await s.FeedbackAsync((await u.GetAsync(ct)).Id, id, body, ct))).RequireAuthorization("feature:chat").WithTags("Quality").Produces<FeedbackDto>();
        var routes = api.MapGroup("/quality").RequireAuthorization("feature:quality").WithTags("Quality");
        routes.MapGet("/retrieval-evals", async (CurrentUser u, RetrievalEvaluationService s, HttpContext http, CancellationToken ct) => { http.Response.Headers.CacheControl = "private, no-store"; return Results.Ok(await s.ListAsync((await u.GetAsync(ct)).Id, ct)); }).Produces<IReadOnlyList<RetrievalEvaluationDto>>();
        routes.MapPost("/retrieval-evals", async (RetrievalEvaluationRequest body, CurrentUser u, RetrievalEvaluationService s, CancellationToken ct) => Results.Ok(await s.CreateAsync((await u.GetAsync(ct)).Id, body, ct))).Produces<RetrievalEvaluationDto>();
        routes.MapGet("/retrieval-evals/{id:guid}", async (Guid id, CurrentUser u, RetrievalEvaluationService s, HttpContext http, CancellationToken ct) => { http.Response.Headers.CacheControl = "private, no-store"; return Results.Ok(await s.ReportAsync((await u.GetAsync(ct)).Id, id, ct)); }).Produces<RetrievalReportDto>();
        routes.MapGet("/retrieval-evals/{id:guid}/report", async (Guid id, CurrentUser u, RetrievalEvaluationService s, HttpContext http, CancellationToken ct) => { http.Response.Headers.CacheControl = "private, no-store"; var report = await s.ReportAsync((await u.GetAsync(ct)).Id, id, ct); return Results.File(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(report, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)), "application/json", $"retrieval-{id:N}.json"); }).Produces<RetrievalReportDto>();
        routes.MapGet("/feedback", async (CurrentUser u, QualityService s, CancellationToken ct) => Results.Ok(await s.FeedbackListAsync((await u.GetAsync(ct)).Id, ct))).Produces<IReadOnlyList<FeedbackDto>>();
        routes.MapGet("/sets", async (CurrentUser u, QualityService s, CancellationToken ct) => Results.Ok(await s.ListAsync((await u.GetAsync(ct)).Id, ct))).Produces<IReadOnlyList<EvaluationSetDto>>();
        routes.MapPost("/sets", async (EvaluationSetRequest body, CurrentUser u, QualityService s, CancellationToken ct) => Results.Ok(await s.SaveAsync((await u.GetAsync(ct)).Id, null, body, ct))).Produces<EvaluationSetDto>();
        routes.MapDelete("/sets/{id:guid}", async (Guid id, CurrentUser u, ResourceLifecycle s, CancellationToken ct) => { await s.DeleteAsync((await u.GetAsync(ct)).Id, id, "evaluation", ct); return Results.NoContent(); });
        routes.MapGet("/sets/{id:guid}", async (Guid id, CurrentUser u, QualityService s, CancellationToken ct) => Results.Ok(await s.GetAsync((await u.GetAsync(ct)).Id, id, ct))).Produces<EvaluationSetDto>();
        routes.MapPut("/sets/{id:guid}", async (Guid id, EvaluationSetRequest body, CurrentUser u, QualityService s, CancellationToken ct) => Results.Ok(await s.SaveAsync((await u.GetAsync(ct)).Id, id, body, ct))).Produces<EvaluationSetDto>();
        routes.MapGet("/sets/{id:guid}/access", async (Guid id, CurrentUser u, ResourceAccess s, CancellationToken ct) => Results.Ok(await s.AclAsync((await u.GetAsync(ct)).Id, id, "evaluation", ct))).Produces<ResourceAclDto>();
        routes.MapPut("/sets/{id:guid}/access", async (Guid id, ResourceAclRequest body, CurrentUser u, ResourceAccess s, CancellationToken ct) => { await s.SetAclAsync((await u.GetAsync(ct)).Id, id, "evaluation", body, ct); return Results.NoContent(); });
        routes.MapPost("/sets/{id:guid}/runs", async (Guid id, EvaluationRunRequest body, CurrentUser u, QualityService s, CancellationToken ct) => Results.Ok(await s.RunAsync((await u.GetAsync(ct)).Id, id, body, ct))).Produces<EvaluationRunDto>();
        routes.MapGet("/sets/{id:guid}/runs", async (Guid id, CurrentUser u, QualityService s, CancellationToken ct) => Results.Ok(await s.RunsAsync((await u.GetAsync(ct)).Id, id, ct))).Produces<IReadOnlyList<EvaluationRunDto>>();
        routes.MapGet("/runs/{id:guid}", async (Guid id, CurrentUser u, QualityService s, HttpContext http, CancellationToken ct) => { http.Response.Headers.CacheControl = "private, no-store"; return Results.Ok(await s.DetailAsync((await u.GetAsync(ct)).Id, id, ct)); }).Produces<EvaluationDetailDto>();
        routes.MapPut("/runs/{id:guid}/results/{caseIndex:int}/{variantIndex:int}/review", async (Guid id, int caseIndex, int variantIndex, ReviewRequest body, CurrentUser u, QualityService s, CancellationToken ct) => { await s.ReviewAsync((await u.GetAsync(ct)).Id, id, caseIndex, variantIndex, body, ct); return Results.NoContent(); });
    }
}
