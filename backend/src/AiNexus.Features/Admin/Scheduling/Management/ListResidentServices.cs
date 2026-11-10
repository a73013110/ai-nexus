using AiNexus.Features.Admin.Scheduling.Engine;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Scheduling;

namespace AiNexus.Features.Admin.Scheduling.Management;

/// <summary>一個常駐服務：宣告（名稱、說明、節奏）加上它在回應請求的這個程序的狀態（見 <see cref="ResidentServiceStates"/>）。</summary>
public sealed record ResidentServiceDto(string Key, string Name, string Category, string Description, string Rhythm, string State, string? Reason);

/// <summary><see cref="Host"/> 是回應請求的主機：狀態在各程序的記憶體，其他站台的要到那個站台看。</summary>
public sealed record ResidentServiceBoardDto(string Host, IReadOnlyList<ResidentServiceDto> Services);

/// <summary>
/// 隨程序常駐的背景服務（佇列消費者、取樣器、日誌管線、排程引擎），和排程任務合起來就是系統會自己做的所有事。
/// 只讀記憶體、不查資料庫；只有營運資料，不含個人資料，所以不稽核。
/// </summary>
internal sealed class ListResidentServices(ResidentServices services) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapGet("", (ListResidentServices handler) => handler.Handle())
        .NotAudited("常駐服務的營運狀態，不含個人資料")
        .WithName("ListResidentServices");

    public ResidentServiceBoardDto Handle() => new(ScheduledTaskRunner.Host,
        [.. services.Read().Select(x => new ResidentServiceDto(
            x.Definition.Key, x.Definition.Name, x.Definition.Category, x.Definition.Description, x.Definition.Rhythm, x.State, x.Reason))]);
}
