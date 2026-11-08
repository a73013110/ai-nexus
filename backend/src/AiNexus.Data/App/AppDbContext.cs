using AiNexus.Data.App.AccessControl;
using AiNexus.Data.App.Artifacts;
using AiNexus.Data.App.Attendance;
using AiNexus.Data.App.Audit;
using AiNexus.Data.App.Billing;
using AiNexus.Data.App.CalendarSync;
using AiNexus.Data.App.Collaboration;
using AiNexus.Data.App.Conversations;
using AiNexus.Data.App.Files;
using AiNexus.Data.App.Identity;
using AiNexus.Data.App.Inference;
using AiNexus.Data.App.Jobs;
using AiNexus.Data.App.Knowledge;
using AiNexus.Data.App.Mail;
using AiNexus.Data.App.Notifications;
using AiNexus.Data.App.Organization;
using AiNexus.Data.App.Projects;
using AiNexus.Data.App.Prompts;
using AiNexus.Data.App.Quality;
using AiNexus.Data.App.Releases;
using AiNexus.Data.App.Repositories;
using AiNexus.Data.App.Scheduling;
using AiNexus.Data.App.Sharing;
using AiNexus.Data.App.WebSearches;
using AiNexus.Platform.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Data.App;

/// <summary>主資料庫的 DbContext；結構以 <c>database/App</c> 為準，這裡只做對應。</summary>
/// <remarks>
/// 實體組態由 <c>ApplyConfigurationsFromAssembly</c> 自動載入，一張表一個檔案（<c>App/&lt;Module&gt;/&lt;Entity&gt;.cs</c>）。
/// 所有時間戳都必須經由 <see cref="Clock"/> 取得，測試才能控制時間。
/// 每張表一個屬性，名稱＝表名，依 schema 分組（<c>DatabaseModelTests</c> 檢查）；新增資料表時在這裡加一行。
/// </remarks>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options, TimeProvider? clock = null) : DbContext(options)
{
    /// <summary>設定檔 <c>Databases</c> 節點中的連線名稱。</summary>
    public const string DatabaseName = "App";

    public TimeProvider Clock { get; } = clock ?? TimeProvider.System;

    public DbSet<Feature> Features => Set<Feature>();
    public DbSet<GroupModelPolicy> GroupModelPolicies => Set<GroupModelPolicy>();
    public DbSet<NavigationItemAudience> NavigationItemAudiences => Set<NavigationItemAudience>();
    public DbSet<NavigationItem> NavigationItems => Set<NavigationItem>();
    public DbSet<RoleGroupFeature> RoleGroupFeatures => Set<RoleGroupFeature>();
    public DbSet<RoleGroupRole> RoleGroupRoles => Set<RoleGroupRole>();
    public DbSet<RoleGroup> RoleGroups => Set<RoleGroup>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserModelPolicy> UserModelPolicies => Set<UserModelPolicy>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();

    public DbSet<ArtifactRevision> ArtifactRevisions => Set<ArtifactRevision>();
    public DbSet<Artifact> Artifacts => Set<Artifact>();

    public DbSet<AttributedPunch> AttributedPunches => Set<AttributedPunch>();
    public DbSet<CardAssignment> CardAssignments => Set<CardAssignment>();
    public DbSet<Card> Cards => Set<Card>();
    public DbSet<Punch> Punches => Set<Punch>();
    public DbSet<PunchFile> PunchFiles => Set<PunchFile>();
    public DbSet<PunchImport> PunchImports => Set<PunchImport>();

    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    public DbSet<ModelCharge> ModelCharges => Set<ModelCharge>();
    public DbSet<ModelPrice> ModelPrices => Set<ModelPrice>();

    public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();
    public DbSet<SyncRun> SyncRuns => Set<SyncRun>();

    public DbSet<ResourceGroup> ResourceGroups => Set<ResourceGroup>();
    public DbSet<ResourceMember> ResourceMembers => Set<ResourceMember>();
    public DbSet<Resource> Resources => Set<Resource>();

    public DbSet<ConversationLabel> ConversationLabels => Set<ConversationLabel>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();

    public DbSet<DiagnosticEvent> DiagnosticEvents => Set<DiagnosticEvent>();

    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<MessageAttachment> MessageAttachments => Set<MessageAttachment>();
    public DbSet<ResourceAttachment> ResourceAttachments => Set<ResourceAttachment>();

    public DbSet<AdministratorBootstrap> AdministratorBootstraps => Set<AdministratorBootstrap>();
    public DbSet<ApiClient> ApiClients => Set<ApiClient>();
    public DbSet<User> Users => Set<User>();
    public DbSet<VerificationChallenge> VerificationChallenges => Set<VerificationChallenge>();

    public DbSet<GenerationRun> GenerationRuns => Set<GenerationRun>();
    public DbSet<ModelInvocation> ModelInvocations => Set<ModelInvocation>();
    public DbSet<ModelProfile> ModelProfiles => Set<ModelProfile>();
    public DbSet<RunEvent> RunEvents => Set<RunEvent>();

    public DbSet<BackgroundJob> BackgroundJobs => Set<BackgroundJob>();

    public DbSet<Chunk> Chunks => Set<Chunk>();
    public DbSet<ChunkEmbedding768> ChunkEmbeddings768 => Set<ChunkEmbedding768>();
    public DbSet<ChunkEmbedding1024> ChunkEmbeddings1024 => Set<ChunkEmbedding1024>();
    public DbSet<Collection> Collections => Set<Collection>();
    public DbSet<ConversationCollection> ConversationCollections => Set<ConversationCollection>();
    public DbSet<DocumentPage> DocumentPages => Set<DocumentPage>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<EmbeddingProfile> EmbeddingProfiles => Set<EmbeddingProfile>();
    public DbSet<MessageCitation> MessageCitations => Set<MessageCitation>();

    public DbSet<DeliveryAttempt> DeliveryAttempts => Set<DeliveryAttempt>();
    public DbSet<DeliverySetting> DeliverySettings => Set<DeliverySetting>();
    public DbSet<OutboundMessageContent> OutboundMessageContents => Set<OutboundMessageContent>();
    public DbSet<OutboundMessage> OutboundMessages => Set<OutboundMessage>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<UnitMember> UnitMembers => Set<UnitMember>();
    public DbSet<Unit> Units => Set<Unit>();

    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectTemplate> ProjectTemplates => Set<ProjectTemplate>();

    public DbSet<PromptTemplate> PromptTemplates => Set<PromptTemplate>();

    public DbSet<EvaluationResult> EvaluationResults => Set<EvaluationResult>();
    public DbSet<EvaluationRun> EvaluationRuns => Set<EvaluationRun>();
    public DbSet<EvaluationSet> EvaluationSets => Set<EvaluationSet>();
    public DbSet<MessageFeedback> MessageFeedback => Set<MessageFeedback>();
    public DbSet<RetrievalEvaluationResult> RetrievalEvaluationResults => Set<RetrievalEvaluationResult>();
    public DbSet<RetrievalEvaluation> RetrievalEvaluations => Set<RetrievalEvaluation>();

    public DbSet<Deployment> Deployments => Set<Deployment>();

    public DbSet<RepositoryConnection> RepositoryConnections => Set<RepositoryConnection>();
    public DbSet<RepositoryFolderImport> RepositoryFolderImports => Set<RepositoryFolderImport>();
    public DbSet<RepositoryImport> RepositoryImports => Set<RepositoryImport>();
    public DbSet<RepositoryPin> RepositoryPins => Set<RepositoryPin>();
    public DbSet<RepositoryReviewResult> RepositoryReviewResults => Set<RepositoryReviewResult>();
    public DbSet<RepositoryReview> RepositoryReviews => Set<RepositoryReview>();

    public DbSet<ScheduledTaskRun> ScheduledTaskRuns => Set<ScheduledTaskRun>();
    public DbSet<ScheduledTask> ScheduledTasks => Set<ScheduledTask>();

    public DbSet<ShareLink> ShareLinks => Set<ShareLink>();
    public DbSet<ShareRecipient> ShareRecipients => Set<ShareRecipient>();

    public DbSet<WebSearch> WebSearches => Set<WebSearch>();

    /// <summary>
    /// <c>inference.udf_UsageEntries</c>：期間 [<paramref name="from"/>, <paramref name="until"/>) 的用量紀錄，<paramref name="until"/> 為 null 表示不限。
    /// 合併規則只寫在函式裡，<c>operations.usp_ListAdminUsers</c> 讀同一份。
    /// </summary>
    [DbFunction("udf_UsageEntries", "inference")]
    public IQueryable<UsageEntry> UsageEntries(DateTimeOffset from, DateTimeOffset? until) => FromExpression(() => UsageEntries(from, until));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
