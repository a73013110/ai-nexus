using AiNexus.Features.Conversations;
using AiNexus.Features.Identity.Users;
using AiNexus.Features.Inference.Models;
using AiNexus.Features.Inference.Runs;
using AiNexus.Platform.Data;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Persistence;

/// <summary>
/// 應用程式的主要 Entity Framework Core 資料庫上下文 (<see cref="DbContext"/>)。
/// </summary>
/// <remarks>
/// 各模組的實體配置由 <c>ApplyConfigurationsFromAssembly</c> 自動掃描載入；跨模組外鍵則集中在 <see cref="CrossModuleRelationships"/>，
/// 避免模組之間因為互相引用實體而產生程式碼循環依賴。
/// 正式環境是 SQL Server；測試改用 SQLite 時，<see cref="SqliteModel"/> 會覆寫 SQLite 不支援的型別。
/// 所有時間戳都必須經由 <see cref="Clock"/> 取得，測試才能控制時間。
/// </remarks>
public sealed class NexusDbContext(DbContextOptions<NexusDbContext> options, TimeProvider? clock = null) : DbContext(options)
{
    /// <summary>
    /// 連線設定檔 <c>Databases</c> 節點中所對應之資料庫名稱鍵值。
    /// </summary>
    public const string DatabaseName = "Nexus";

    public TimeProvider Clock { get; } = clock ?? TimeProvider.System;

    public DbSet<NexusUser> Users => Set<NexusUser>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<GenerationRun> Runs => Set<GenerationRun>();
    public DbSet<RunEvent> RunEvents => Set<RunEvent>();
    public DbSet<ModelProfile> ModelProfiles => Set<ModelProfile>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NexusDbContext).Assembly);

        if (Database.ProviderName == SqliteModel.ProviderName)
        {
            SqliteModel.Configure(modelBuilder);
        }
    }
}

/// <summary>
/// 儲存層連線設定就緒檢測器。
/// </summary>
/// <remarks>
/// Testing 環境一律視為已設定；其他環境必須在設定檔中提供 <c>Databases:Nexus</c> 連線資訊。
/// </remarks>
public sealed class StorageReadiness(IOptions<DatabasesOptions> databases, IHostEnvironment environment)
{
    public bool Configured => environment.IsEnvironment("Testing") || databases.Value.For(NexusDbContext.DatabaseName).Configured;

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
