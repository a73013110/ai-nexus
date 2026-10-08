using AiNexus.Features.Conversations;
using AiNexus.Features.Identity;
using AiNexus.Features.Inference;
using AiNexus.Features.Operations;
using AiNexus.Features.AccessControl;
using Microsoft.EntityFrameworkCore;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Persistence;

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
        PrepareAudits(); return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        PrepareAudits(); return base.SaveChanges(acceptAllChangesOnSuccess);
    }
    private void PrepareAudits()
    {
        foreach (var entry in ChangeTracker.Entries<AuditEvent>().Where(x => x.State == EntityState.Added))
        {
            entry.Entity.DetailsJson = AiNexus.Platform.Diagnostics.AuditRedactor.Sanitize(entry.Entity.DetailsJson);
            entry.Entity.Action = AiNexus.Platform.Diagnostics.DiagnosticRedactor.Text(entry.Entity.Action, 64);
            entry.Entity.Result = entry.Entity.Result is null ? null : AiNexus.Platform.Diagnostics.DiagnosticRedactor.Text(entry.Entity.Result, 80);
            entry.Entity.TraceId ??= System.Diagnostics.Activity.Current?.TraceId.ToHexString();
            entry.Entity.OperationId ??= Guid.TryParse(System.Diagnostics.Activity.Current?.GetTagItem("operation.id")?.ToString(), out var operation) ? operation : null;
        }
        if (Guid.TryParse(http?.HttpContext?.User.FindFirst(SessionIdentity.ActorId)?.Value, out var actor))
            foreach (var entry in ChangeTracker.Entries<AuditEvent>().Where(x => x.State == EntityState.Added)) entry.Entity.ActorId ??= actor;
    }

    protected override void OnModelCreating(ModelBuilder model)
    {
        AiNexus.Features.Diagnostics.DiagnosticConfiguration.Configure(model);
        AccessControlConfiguration.Configure(model);
        AiNexus.Features.Administration.AdministrationConfiguration.Configure(model);
        AiNexus.Features.Collaboration.CollaborationConfiguration.Configure(model);
        BackgroundJobConfiguration.Configure(model);
        model.ApplyConfiguration(new AiNexus.Features.Notifications.WorkspaceNotificationConfiguration());
        ModelInvocationConfiguration.Configure(model);
        model.ApplyConfiguration(new AiNexus.Features.Billing.ModelPriceConfiguration());
        model.ApplyConfiguration(new AiNexus.Features.Billing.ModelChargeConfiguration());
        model.ApplyConfiguration(new AiNexus.Features.WebSearch.WebSearchRecordConfiguration());
        AiNexus.Features.Repositories.RepositoryConfiguration.Configure(model);
        AiNexus.Features.Repositories.RepositoryReviewConfiguration.Configure(model);
        AiNexus.Features.Knowledge.KnowledgeConfiguration.Configure(model, Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite");
        AiNexus.Features.Artifacts.ArtifactConfiguration.Configure(model);
        AiNexus.Features.Projects.ProjectConfiguration.Configure(model);
        AiNexus.Features.Sharing.SharingConfiguration.Configure(model);
        AiNexus.Features.Quality.QualityConfiguration.Configure(model);
        model.ApplyConfiguration(new AiNexus.Features.Integrations.ImportedSourceReferenceConfiguration());
        PlatformFeatures.Add(model, "integrations", "資料來源", "/integrations", 80, administratorsOnly: true);
        PlatformFeatures.Add(model, "quality", "品質評測", "/quality", 60);
        PlatformFeatures.Add(model, "shared", "分享", "/shared", 50);
        PlatformFeatures.Add(model, "projects", "專案", "/projects", 20);
        PlatformFeatures.Add(model, "dashboard", "總覽", "/dashboard", 5);
        PlatformFeatures.Add(model, "monitoring", "即時監控", "/admin/monitoring", 91, administratorsOnly: true);
        PlatformFeatures.Add(model, ActivityAuditEndpoints.Feature, "活動稽核", "/admin/audit", 92, administratorsOnly: true);
        PlatformFeatures.Add(model, "repositories", "程式庫", "/repositories", 65);
        AiNexus.Features.Attachments.AttachmentReferenceConfiguration.Configure(model);
        PlatformFeatures.Add(model, "knowledge", "知識庫", "/knowledge", 30);
        PlatformFeatures.Add(model, "tasks", "背景任務", "/tasks", 70);
        PlatformFeatures.Add(model, "artifacts", "成果文件", "/artifacts", 40);
        ConversationConfiguration.Configure(model);
        AiNexus.Features.Attachments.AttachmentConfiguration.Configure(model);
        model.ApplyConfiguration(new AiNexus.Features.Library.PromptTemplateConfiguration());
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
            p.Property(x => x.ReadingFontSize).HasDefaultValue(UserPreferences.DefaultReadingFontSize);
            p.Property(x => x.ReadingLineHeight).HasDefaultValue(UserPreferences.DefaultReadingLineHeight);
            p.Property(x => x.SidebarWidth).HasDefaultValue(UserPreferences.DefaultSidebarWidth);
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
        run.Property(x => x.IssueCode).HasMaxLength(40); run.Property(x => x.TraceId).HasMaxLength(32); run.Property(x => x.ParentSpanId).HasMaxLength(16);
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
        runEvent.Property(x => x.IssueCode).HasMaxLength(40);
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
        audit.Property(x => x.TraceId).HasMaxLength(32); audit.Property(x => x.IssueCode).HasMaxLength(40);
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
