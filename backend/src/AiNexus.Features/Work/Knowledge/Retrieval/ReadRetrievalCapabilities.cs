using AiNexus.Data.App;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Work.Knowledge.Retrieval;

/// <summary>
/// SQL Server 向量能力與 embedding／rerank 模型狀態。<c>ReadAsync</c> 只回傳上次的探測結果，<c>ProbeAsync</c> 才會即時呼叫模型。
/// </summary>
internal sealed class ReadRetrievalCapabilities(SqlVectorCapabilities sql, RetrievalModelProbe probe) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes)
    {
        routes.MapGet("/capabilities", (ReadRetrievalCapabilities handler, CancellationToken ct) =>
            handler.ReadAsync(ct))
            .NotAudited("檢索功能可用狀態")
            .WithName("GetRetrievalCapabilities");

        routes.MapPost("/capabilities/probe", (ICurrentUser user, ReadRetrievalCapabilities handler, CancellationToken ct) =>
            handler.ProbeAsync(user.Id, ct))
            .Audited(KnowledgeAudit.RetrievalModelDiagnosed)
            .WithName("ProbeRetrievalModels");
    }

    public Task<RetrievalCapabilitiesDto> ReadAsync(CancellationToken ct) => DescribeAsync(probe.Embedding, probe.Rerank, ct);

    public async Task<RetrievalCapabilitiesDto> ProbeAsync(Guid actor, CancellationToken ct)
    {
        var result = await probe.CheckAsync(actor, ct);
        return await DescribeAsync(result.Embedding, result.Rerank, ct);
    }

    private async Task<RetrievalCapabilitiesDto> DescribeAsync(RetrievalConnectionDto embedding, RetrievalConnectionDto rerank, CancellationToken ct)
        => new(await sql.ReadAsync(ct), embedding, rerank);
}
