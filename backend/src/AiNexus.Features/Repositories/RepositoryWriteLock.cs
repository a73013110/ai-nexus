namespace AiNexus.Features.Repositories;

/// <summary>序列化本模組的寫入（連線、匯入、建立 review）。鎖只在單一程序內有效；系統只部署單一 IIS 執行個體，所以足夠。</summary>
public sealed class RepositoryWriteLock
{
    public SemaphoreSlim Gate { get; } = new(1, 1);
}
