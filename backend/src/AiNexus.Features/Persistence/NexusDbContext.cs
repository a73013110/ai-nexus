using AiNexus.Features.Conversations;
using AiNexus.Features.Identity.Users;
using AiNexus.Features.Inference;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Persistence;

/// <summary>
/// The application's single DbContext. Mappings are <c>IEntityTypeConfiguration&lt;T&gt;</c> classes next to each entity,
/// cross-module foreign keys are in <see cref="CrossModuleRelationships"/> and SQLite-only differences in
/// <see cref="SqliteModel"/>. Save-time behavior comes from the interceptors added by
/// <see cref="NexusDbContextOptions.AddNexusInterceptors"/>.
/// </summary>
public sealed class NexusDbContext(DbContextOptions<NexusDbContext> options) : DbContext(options)
{
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
        if (Database.ProviderName == SqliteModel.ProviderName) SqliteModel.Configure(modelBuilder);
    }
}

public sealed class StorageReadiness(IConfiguration configuration, IHostEnvironment environment)
{
    public bool Configured => environment.IsEnvironment("Testing") || !string.IsNullOrWhiteSpace(configuration.GetConnectionString("Nexus"));
    public void RequireConfigured()
    {
        if (!Configured) throw new ApiException(503, "storage_not_configured", "伺服器尚未完成資料庫設定。請由管理員設定 SQL 連線。");
    }
}
