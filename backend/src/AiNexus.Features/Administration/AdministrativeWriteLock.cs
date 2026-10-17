namespace AiNexus.Features.Administration;

/// <summary>
/// 行政變更的串行化鎖。目前部署為單一 IIS 執行個體，程序內的 semaphore 即足夠；
/// 若改為多執行個體，這把鎖會失效，需改用資料庫層級的鎖。
/// </summary>
public sealed class AdministrativeWriteLock { public SemaphoreSlim Gate { get; } = new(1, 1); }
