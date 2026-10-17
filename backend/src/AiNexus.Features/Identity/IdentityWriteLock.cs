namespace AiNexus.Features.Identity;

/// <summary>身分相關寫入（登入狀態更新、登入時的授權寫入）的程序內串行化鎖。部署為單一執行個體，因此不需要分散式鎖。</summary>
public sealed class IdentityWriteLock
{
    public SemaphoreSlim Gate { get; } = new(1, 1);
}
