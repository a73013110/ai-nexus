using AiNexus.Platform.Data;
using AiNexus.Platform.Errors;
using Microsoft.Extensions.Options;

namespace AiNexus.Data.App;

/// <summary>主資料庫的連線是否已設定。</summary>
/// <remarks>
/// Testing 環境一律視為已設定；其他環境必須在設定檔中提供 <c>Databases:Nexus</c> 連線資訊。
/// </remarks>
public sealed class StorageReadiness(IOptions<DatabasesOptions> databases, IHostEnvironment environment)
{
    public bool Configured => environment.IsEnvironment("Testing") || databases.Value.For(AppDbContext.DatabaseName).Configured;

    /// <summary>
    /// 檢查資料庫設定是否已配置，若未就緒則立即拋出安全之 503 <see cref="ExternalServiceException"/>。
    /// </summary>
    public void RequireConfigured()
    {
        if (!Configured)
        {
            throw new ExternalServiceException(
                Error.Unavailable("storage_not_configured"),
                "伺服器尚未完成資料庫設定。請由管理員設定 SQL 連線。");
        }
    }
}
