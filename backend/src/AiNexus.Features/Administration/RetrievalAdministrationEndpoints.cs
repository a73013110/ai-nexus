using AiNexus.Features.Persistence;
using AiNexus.Features.Identity;
using AiNexus.Features.Operations;
using AiNexus.Features.Knowledge;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Administration;

public sealed record AdminRetrievalSearchRequest(string Query, IReadOnlyList<Guid> CollectionIds, string? Mode = null, bool? Rerank = null);
public static class RetrievalAdministrationEndpoints
{
    public static void MapRetrievalAdministration(this RouteGroupBuilder api)
    {
        var routes = api.MapGroup("/knowledge");
        routes.MapGet("/profiles", async (EmbeddingLifecycle service, CancellationToken ct) => Results.Ok(await service.ListAsync(ct))).WithName("ListEmbeddingProfiles").Produces<IReadOnlyList<EmbeddingProfileDto>>();
        routes.MapPost("/profiles/{id:int}/rebuild", async (int id, EmbeddingLifecycle service, CancellationToken ct) => Results.Ok(await service.StartAsync(id, ct))).WithName("RebuildEmbeddingProfile").Produces<JobDto>();
        routes.MapPost("/profiles/{id:int}/activate", async (int id, EmbeddingLifecycle service, CancellationToken ct) => { await service.ActivateAsync(id, ct); return Results.NoContent(); }).WithName("ActivateEmbeddingProfile");
        routes.MapDelete("/profiles/{id:int}/vectors", async (int id, EmbeddingLifecycle service, CancellationToken ct) => { await service.ClearAsync(id, ct); return Results.NoContent(); }).WithName("ClearRetiredEmbeddingVectors");
        routes.MapGet("/capabilities", async (NexusDbContext db, SqlVectorCapabilities sql, RetrievalModelProbe probe, CancellationToken ct) =>
            Results.Ok(new RetrievalCapabilitiesDto(db.Database.IsSqlServer() ? await sql.ReadAsync(ct) : null, !db.Database.IsSqlServer(), probe.Embedding, probe.Rerank))).WithName("GetRetrievalCapabilities").Produces<RetrievalCapabilitiesDto>();
        routes.MapPost("/capabilities/probe", async (NexusDbContext db, SqlVectorCapabilities sql, RetrievalModelProbe probe, CurrentUser current, CancellationToken ct) => {
            var result = await probe.CheckAsync((await current.GetAsync(ct)).Id, ct);
            return Results.Ok(new RetrievalCapabilitiesDto(db.Database.IsSqlServer() ? await sql.ReadAsync(ct) : null, !db.Database.IsSqlServer(), result.Embedding, result.Rerank));
        }).WithName("ProbeRetrievalModels").Produces<RetrievalCapabilitiesDto>();
        routes.MapPost("/search", async (AdminRetrievalSearchRequest body, RetrievalPipeline pipeline, CurrentUser current, CancellationToken ct) =>
            Results.Ok(await pipeline.SearchAsync((await current.GetAsync(ct)).Id, new(body.Query, body.CollectionIds), ct, mode: body.Mode, rerank: body.Rerank))).WithName("TestAdminRetrieval").Produces<KnowledgeSearchDto>();
    }
}
