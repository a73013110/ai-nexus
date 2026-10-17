using AiNexus.Features.Identity.Authentication;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Data.Sql;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Inference.Runs;

/// <summary>
/// 系統運行健康狀態與生成排隊統計資料傳輸物件。
/// </summary>
public sealed record StatusDto(
    string Storage,
    string Authentication,
    int QueueDepth,
    bool Generating);

/// <summary>
/// 系統全域運作狀態查詢端點。
/// 回傳資料庫連線、生成工作者就緒狀態、身分驗證模式與當前生成佇列深度。
/// </summary>
internal sealed class GetStatus(
    GenerationScheduler scheduler,
    ISqlDatabase<NexusDbContext> db,
    IOptions<AdAuthenticationOptions> auth)
{
    public static void Map(RouteGroupBuilder api) => api
        .MapGet("/status", async (GetStatus handler, CancellationToken ct) =>
            TypedResults.Ok(await handler.HandleAsync(ct)))
        .WithName("GetStatus")
        .WithTags("Operations");

    public async Task<StatusDto> HandleAsync(CancellationToken ct)
    {
        // 透過 5 秒短超時的輕量查詢測試資料庫連線可用性
        var connected = await db.QuerySingleAsync<int>("SELECT 1", commandTimeout: 5, cancellationToken: ct) == 1;

        var storageStatus = connected && scheduler.Ready ? "ready" : "unavailable";
        var authMode = auth.Value.Mode.ToLowerInvariant();

        return new StatusDto(
            storageStatus,
            authMode,
            scheduler.QueueDepth,
            scheduler.Generating);
    }
}
