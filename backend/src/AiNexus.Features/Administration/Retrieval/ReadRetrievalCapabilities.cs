using AiNexus.Data.App;
using AiNexus.Features.Identity;
using AiNexus.Features.Knowledge.Retrieval;

namespace AiNexus.Features.Administration.Retrieval;

/// <summary>
/// SQL Server 向量能力與 embedding／rerank 模型狀態。<c>ReadAsync</c> 只回傳上次的探測結果，<c>ProbeAsync</c> 才會即時呼叫模型。
/// </summary>
internal sealed class ReadRetrievalCapabilities(SqlVectorCapabilities sql, RetrievalModelProbe probe)
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
        => new(await sql.ReadAsync(ct), embedding, rerank);
}
