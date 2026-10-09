using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;
using AiNexus.Features.Knowledge.Retrieval;

namespace AiNexus.Features.Administration;

/// <summary>SQL Server vector support and the configured embedding and rerank models, as last seen or probed now.</summary>
internal static class ReadRetrievalCapabilities
{
    public static void MapGet(RouteGroupBuilder routes) => routes
        .MapGet("/capabilities", async (NexusDbContext db, SqlVectorCapabilities sql, RetrievalModelProbe probe, CancellationToken ct) =>
            Results.Ok(new RetrievalCapabilitiesDto(db.Database.IsSqlServer() ? await sql.ReadAsync(ct) : null, !db.Database.IsSqlServer(), probe.Embedding, probe.Rerank)))
        .WithName("GetRetrievalCapabilities").Produces<RetrievalCapabilitiesDto>();

    public static void MapProbe(RouteGroupBuilder routes) => routes
        .MapPost("/capabilities/probe", async (NexusDbContext db, SqlVectorCapabilities sql, RetrievalModelProbe probe, ICurrentUser user, CancellationToken ct) =>
        {
            var result = await probe.CheckAsync(user.Id, ct);
            return Results.Ok(new RetrievalCapabilitiesDto(db.Database.IsSqlServer() ? await sql.ReadAsync(ct) : null, !db.Database.IsSqlServer(), result.Embedding, result.Rerank));
        })
        .WithName("ProbeRetrievalModels").Produces<RetrievalCapabilitiesDto>();
}
