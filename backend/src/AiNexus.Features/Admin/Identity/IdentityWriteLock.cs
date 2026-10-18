namespace AiNexus.Features.Admin.Identity;

/// <summary>身分寫入（建立、綁定、更新使用者，與登入時套用授權）的程序內串行化鎖。目前為單一執行個體，故不需要分散式鎖。</summary>
public sealed class IdentityWriteLock
{
    public SemaphoreSlim Gate { get; } = new(1, 1);
}
