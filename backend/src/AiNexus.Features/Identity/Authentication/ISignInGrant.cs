using AiNexus.Data.App.Identity;

namespace AiNexus.Features.Identity.Authentication;

/// <summary>
/// 登入時套用的額外授權（例如設定中的初始管理員）。由 Identity 之上的模組實作，登入流程本身因此不依賴那些模組。
/// </summary>
public interface ISignInGrant
{
    /// <summary>回報 <see cref="ApplyAsync"/> 是否會實際授權；為 true 時登入必須先取得 identity 寫入鎖。</summary>
    Task<bool> PendingAsync(User user, CancellationToken ct);

    /// <summary>在 identity 寫入鎖內呼叫。</summary>
    Task ApplyAsync(User user, CancellationToken ct);
}
