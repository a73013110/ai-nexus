using AiNexus.Modules.Conversations;
using AiNexus.Modules.Identity;
using AiNexus.Modules.Inference;
using AiNexus.Modules.Operations;
using AiNexus.Modules.AccessControl;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.BuildingBlocks;

public sealed class NexusDbContext(DbContextOptions<NexusDbContext> options, IHttpContextAccessor? http = null) : DbContext(options)
{
    public DbSet<NexusUser> Users => Set<NexusUser>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<GenerationRun> Runs => Set<GenerationRun>();
    public DbSet<RunEvent> RunEvents => Set<RunEvent>();
    public DbSet<ModelProfile> ModelProfiles => Set<ModelProfile>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        if (Guid.TryParse(http?.HttpContext?.User.FindFirst(SessionIdentity.ActorId)?.Value, out var actor))
            foreach (var entry in ChangeTracker.Entries<AuditEvent>().Where(x => x.State == EntityState.Added)) entry.Entity.ActorId ??= actor;
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder model)
    {
        AccessControlConfiguration.Configure(model);
        AiNexus.Modules.Administration.AdministrationConfiguration.Configure(model);
        AiNexus.Modules.Collaboration.CollaborationConfiguration.Configure(model);
        BackgroundJobConfiguration.Configure(model);
        ModelInvocationConfiguration.Configure(model);
        AiNexus.Modules.Billing.BillingConfiguration.Configure(model);
        AiNexus.Modules.WebSearch.WebSearchConfiguration.Configure(model);
        AiNexus.Modules.Repositories.RepositoryConfiguration.Configure(model);
        AiNexus.Modules.Knowledge.KnowledgeConfiguration.Configure(model);
        AiNexus.Modules.Artifacts.ArtifactConfiguration.Configure(model);
        AiNexus.Modules.Projects.ProjectConfiguration.Configure(model);
        AiNexus.Modules.Sharing.SharingConfiguration.Configure(model);
        AiNexus.Modules.Quality.QualityConfiguration.Configure(model);
        AiNexus.Modules.Integrations.IntegrationConfiguration.Configure(model);
        PlatformFeatures.Add(model, "integrations", "來源", "/integrations", 80, administratorsOnly: true);
        PlatformFeatures.Add(model, "quality", "評測", "/quality", 60);
        PlatformFeatures.Add(model, "shared", "分享", "/shared", 50);
        PlatformFeatures.Add(model, "projects", "專案", "/projects", 20);
        PlatformFeatures.Add(model, "dashboard", "總覽", "/dashboard", 5);
        PlatformFeatures.Add(model, "repositories", "程式庫", "/repositories", 65);
        AiNexus.Modules.Attachments.AttachmentReferenceConfiguration.Configure(model);
        PlatformFeatures.Add(model, "knowledge", "知識", "/knowledge", 30);
        PlatformFeatures.Add(model, "tasks", "任務", "/tasks", 70);
        PlatformFeatures.Add(model, "artifacts", "成果", "/artifacts", 40);
        ConversationConfiguration.Configure(model);
        AiNexus.Modules.Attachments.AttachmentConfiguration.Configure(model);
        AiNexus.Modules.Library.LibraryConfiguration.Configure(model);
        var user = model.Entity<NexusUser>();
        user.ToTable("Users", "identity", table => table.HasCheckConstraint("CK_Users_AttachmentLimitBytes", "[AttachmentLimitBytes] IS NULL OR [AttachmentLimitBytes] BETWEEN 0 AND 1000000000000000"));
        user.HasKey(x => x.Id);
        user.Property(x => x.Sid).HasMaxLength(184);
        user.HasIndex(x => x.Sid).IsUnique();
        user.Property(x => x.Account).HasMaxLength(256);
        user.Property(x => x.DisplayName).HasMaxLength(256);
        user.Property(x => x.Enabled).HasDefaultValue(true);
        user.Property(x => x.AdEnabled).HasDefaultValue(true);
        user.Property(x => x.AdAccount).HasMaxLength(64);
        user.Property(x => x.LocalAccount).HasMaxLength(64);
        user.Property(x => x.PasswordHash).HasMaxLength(512);
        user.Property(x => x.SecurityVersion).IsConcurrencyToken();
        user.HasIndex(x => x.AdAccount).IsUnique().HasFilter("[AdAccount] IS NOT NULL");
        user.HasIndex(x => x.LocalAccount).IsUnique().HasFilter("[LocalAccount] IS NOT NULL");
        user.OwnsOne(x => x.Preferences, p =>
        {
            p.ToTable("UserPreferences", "identity");
            p.Property(x => x.Theme).HasMaxLength(12);
            p.Property(x => x.DefaultModelId).HasMaxLength(160);
            p.Property(x => x.Density).HasMaxLength(16);
            p.Property(x => x.ReadingWidth).HasMaxLength(16);
            p.Property(x => x.DefaultReasoningEffort).HasMaxLength(16);
            p.Property(x => x.Density).HasDefaultValue("comfortable");
            p.Property(x => x.ReadingWidth).HasDefaultValue("standard");
            p.Property(x => x.DefaultReasoningEffort).HasDefaultValue("auto");
            p.Property(x => x.ReadingFontSize).HasDefaultValue(17);
            p.Property(x => x.ReadingLineHeight).HasDefaultValue(1.8);
            p.Property(x => x.SidebarWidth).HasDefaultValue(264);
            p.Property(x => x.EnterToSend).HasDefaultValue(true);
            p.Property(x => x.AutoFollow).HasDefaultValue(true);
            p.Property(x => x.SaveLocalDrafts).HasDefaultValue(true);
        });
        var run = model.Entity<GenerationRun>();
        run.ToTable("GenerationRuns", "inference");
        run.HasKey(x => x.Id);
        run.Property(x => x.ModelId).HasMaxLength(160);
        run.Property(x => x.Provider).HasMaxLength(32);
        run.Property(x => x.ProviderModelId).HasMaxLength(150);
        run.Property(x => x.Status).HasMaxLength(16);
        run.Property(x => x.ErrorCode).HasMaxLength(80);
        run.Property(x => x.IdempotencyKey).HasMaxLength(80);
        run.Property(x => x.RequestHash).HasMaxLength(64);
        run.HasIndex(x => new { x.OwnerId, x.IdempotencyKey }).IsUnique();
        run.HasIndex(x => x.ActiveOwnerId).IsUnique().HasFilter("[ActiveOwnerId] IS NOT NULL");
        run.HasIndex(x => new { x.ConversationId, x.CreatedAt });
        run.HasIndex(x => new { x.ActiveOwnerId, x.LeaseExpiresAt });
        run.HasOne<Conversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Restrict);
        run.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        run.HasOne<Message>().WithMany().HasForeignKey(x => x.UserMessageId).OnDelete(DeleteBehavior.Restrict);
        run.HasOne<Message>().WithMany().HasForeignKey(x => x.AssistantMessageId).OnDelete(DeleteBehavior.Restrict);
        var runEvent = model.Entity<RunEvent>();
        runEvent.ToTable("RunEvents", "inference");
        runEvent.HasKey(x => new { x.RunId, x.Sequence });
        runEvent.Property(x => x.Type).HasMaxLength(16);
        runEvent.Property(x => x.Status).HasMaxLength(16);
        runEvent.Property(x => x.ErrorCode).HasMaxLength(80);
        runEvent.HasOne<GenerationRun>().WithMany().HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Cascade);
        var profile = model.Entity<ModelProfile>();
        profile.ToTable("ModelProfiles", "inference");
        profile.HasKey(x => x.Id);
        profile.Property(x => x.Id).HasMaxLength(160);
        profile.Property(x => x.DisplayName).HasMaxLength(120);
        profile.Property(x => x.Provider).HasMaxLength(32);
        profile.Property(x => x.ProviderModelId).HasMaxLength(150);
        var audit = model.Entity<AuditEvent>();
        audit.ToTable("AuditEvents", "operations");
        audit.HasKey(x => x.Id);
        audit.Property(x => x.Action).HasMaxLength(64);
        audit.Property(x => x.Result).HasMaxLength(80);
        audit.Property(x => x.DetailsJson).HasMaxLength(40000);
        audit.HasIndex(x => x.At);
        audit.HasIndex(x => new { x.Action, x.Id });
        audit.HasIndex(x => new { x.ResourceId, x.Id });
        audit.HasIndex(x => new { x.ActorId, x.Id });
        DatabaseDescriptions.Configure(model);
        // SQLite is used only by relational integration tests; it lacks native offset ordering.
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            foreach (var entity in model.Model.GetEntityTypes())
                foreach (var property in entity.GetProperties())
                    if (property.ClrType == typeof(DateTimeOffset) || property.ClrType == typeof(DateTimeOffset?))
                        property.SetValueConverter(new Microsoft.EntityFrameworkCore.Storage.ValueConversion.DateTimeOffsetToBinaryConverter());
        }
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
