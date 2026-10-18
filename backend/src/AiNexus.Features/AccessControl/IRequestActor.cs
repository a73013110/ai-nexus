namespace AiNexus.Features.AccessControl;

/// <summary>
/// 目前請求的執行者，供 AccessControl 自己的管理端點取得 actor id（其他模組用 Identity 的 <c>ICurrentUser</c>）。
/// 由 Identity 實作，並與 <c>ICurrentUser</c> 一樣由 <c>/api/v1</c> 的 filter 在 handler 執行前解析；AccessControl 因此不必依賴 Identity。
/// </summary>
public interface IRequestActor
{
    Guid Id { get; }
}
