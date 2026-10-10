using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;
using AiNexus.Features.Knowledge.Retrieval;

namespace AiNexus.Features.Administration.Retrieval;

/// <summary>SQL Server vector support and the configured embedding and rerank models, as last seen or probed now.</summary>
internal sealed class ReadRetrievalCapabilities(NexusDbContext db, SqlVectorCapabilities sql, RetrievalModelProbe probe)
{
    public static void MapGet(RouteGroupBuilder routes) => routes
        .MapGet("/capabilities", async (ReadRetrievalCapabilities handler, CancellationToken ct) => TypedResults.Ok(await handler.ReadAsync(ct)))
        .WithName("GetRetrievalCapabilities");

    public static void MapProbe(RouteGroupBuilder routes) => routes
        .MapPost("/capabilities/probe", async (ICurrentUser user, ReadRetrievalCapabilities handler, CancellationToken ct) => TypedResults.Ok(await handler.ProbeAsync(user.Id, ct)))
        .WithName("ProbeRetrievalModels");

    public Task<RetrievalCapabilitiesDto> ReadAsync(CancellationToken ct) => DescribeAsync(probe.Embedding, probe.Rerank, ct);

    public async Task<RetrievalCapabilitiesDto> ProbeAsync(Guid actor, CancellationToken ct)
    {
        var result = await probe.CheckAsync(actor, ct);
        return await DescribeAsync(result.Embedding, result.Rerank, ct);
    }

    private async Task<RetrievalCapabilitiesDto> DescribeAsync(RetrievalConnectionDto embedding, RetrievalConnectionDto rerank, CancellationToken ct)
        => new(db.Database.IsSqlServer() ? await sql.ReadAsync(ct) : null, !db.Database.IsSqlServer(), embedding, rerank);
}
