namespace AiNexus.Features.Administration;

/// <summary>
/// 行政變更的串行化鎖。部署為單一 IIS 執行個體，所以程序內的 semaphore 就足夠；若日後多執行個體，這把鎖會失效，需改用資料庫層級的鎖。
/// </summary>
public sealed class AdministrativeWriteLock { public SemaphoreSlim Gate { get; } = new(1, 1); }
