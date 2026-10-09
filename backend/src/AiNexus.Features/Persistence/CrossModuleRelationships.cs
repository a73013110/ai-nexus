using AiNexus.Features.AccessControl;
using AiNexus.Features.Administration;
using AiNexus.Features.Artifacts;
using AiNexus.Features.Attachments;
using AiNexus.Features.Billing;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Conversations;
using AiNexus.Features.Inference;
using AiNexus.Features.Integrations;
using AiNexus.Features.Library;
using AiNexus.Features.Notifications;
using AiNexus.Features.Projects;
using AiNexus.Features.Repositories;
using AiNexus.Features.Sharing;
using AiNexus.Features.WebSearch;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AiNexus.Features.Jobs;
using AiNexus.Features.Knowledge.Collections;
using AiNexus.Features.Knowledge.Documents;
using AiNexus.Features.Knowledge.Retrieval;
using AiNexus.Features.Identity.Users;
using AiNexus.Features.Quality.Feedback;
using AiNexus.Features.Quality.Evaluations;
using AiNexus.Features.Quality.RetrievalEvaluations;

namespace AiNexus.Features.Persistence;

/// <summary>
/// Foreign keys whose two ends live in different modules. A module's own entity configuration only references its own
/// entities, so the database constraints stay intact without creating code dependencies between modules. Each overload
/// configures the dependent entity; <c>ApplyConfigurationsFromAssembly</c> applies one per implemented interface.
/// </summary>
internal sealed class CrossModuleRelationships :
    IEntityTypeConfiguration<UserRole>,
    IEntityTypeConfiguration<AdministratorBootstrap>,
    IEntityTypeConfiguration<UserModelPolicy>,
    IEntityTypeConfiguration<Artifact>,
    IEntityTypeConfiguration<ArtifactRevision>,
    IEntityTypeConfiguration<Attachment>,
    IEntityTypeConfiguration<AttachmentReference>,
    IEntityTypeConfiguration<MessageAttachment>,
    IEntityTypeConfiguration<ModelCharge>,
    IEntityTypeConfiguration<ModelPrice>,
    IEntityTypeConfiguration<ResourceGroup>,
    IEntityTypeConfiguration<ResourceMember>,
    IEntityTypeConfiguration<WorkspaceResource>,
    IEntityTypeConfiguration<Conversation>,
    IEntityTypeConfiguration<ModelInvocation>,
    IEntityTypeConfiguration<ImportedSourceReference>,
    IEntityTypeConfiguration<ConversationKnowledge>,
    IEntityTypeConfiguration<KnowledgeCollection>,
    IEntityTypeConfiguration<KnowledgeDocument>,
    IEntityTypeConfiguration<MessageCitation>,
    IEntityTypeConfiguration<PromptTemplate>,
    IEntityTypeConfiguration<WorkspaceNotification>,
    IEntityTypeConfiguration<BackgroundJob>,
    IEntityTypeConfiguration<Project>,
    IEntityTypeConfiguration<EvaluationResult>,
    IEntityTypeConfiguration<EvaluationRun>,
    IEntityTypeConfiguration<EvaluationSet>,
    IEntityTypeConfiguration<MessageFeedback>,
    IEntityTypeConfiguration<RetrievalEvaluation>,
    IEntityTypeConfiguration<RepositoryConnection>,
    IEntityTypeConfiguration<RepositoryImport>,
    IEntityTypeConfiguration<RepositoryReview>,
    IEntityTypeConfiguration<ShareLink>,
    IEntityTypeConfiguration<ShareRecipient>,
    IEntityTypeConfiguration<WebSearchRecord>,
    IEntityTypeConfiguration<GenerationRun>
{
    public void Configure(EntityTypeBuilder<UserRole> userRole) => userRole.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);

    public void Configure(EntityTypeBuilder<AdministratorBootstrap> administratorBootstrap) => administratorBootstrap.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);

    public void Configure(EntityTypeBuilder<UserModelPolicy> userModelPolicy) => userModelPolicy.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);

    public void Configure(EntityTypeBuilder<Artifact> artifact)
    {
        artifact.HasOne<WorkspaceResource>().WithMany().HasForeignKey(x => x.Id).OnDelete(DeleteBehavior.Restrict);
        artifact.HasOne<Message>().WithMany().HasForeignKey(x => x.SourceMessageId).OnDelete(DeleteBehavior.Restrict);
        // An artifact may belong to a project; ResourceLifecycle detaches artifacts before a project is deleted.
        artifact.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<ArtifactRevision> artifactRevision) => artifactRevision.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Restrict);

    public void Configure(EntityTypeBuilder<Attachment> attachment) => attachment.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);

    public void Configure(EntityTypeBuilder<AttachmentReference> attachmentReference) => attachmentReference.HasOne<WorkspaceResource>().WithMany().HasForeignKey(x => x.ResourceId).OnDelete(DeleteBehavior.Restrict);

    public void Configure(EntityTypeBuilder<MessageAttachment> messageAttachment) => messageAttachment.HasOne<Message>().WithMany().HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Restrict);

    public void Configure(EntityTypeBuilder<ModelCharge> modelCharge)
    {
        modelCharge.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        modelCharge.HasOne<Conversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<ModelPrice> modelPrice) => modelPrice.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);

    public void Configure(EntityTypeBuilder<ResourceGroup> resourceGroup) => resourceGroup.HasOne<RoleGroup>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);

    public void Configure(EntityTypeBuilder<ResourceMember> resourceMember) => resourceMember.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);

    public void Configure(EntityTypeBuilder<WorkspaceResource> workspaceResource) => workspaceResource.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);

    public void Configure(EntityTypeBuilder<Conversation> conversation)
    {
        conversation.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        conversation.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<ModelInvocation> modelInvocation) => modelInvocation.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);

    public void Configure(EntityTypeBuilder<ImportedSourceReference> importedSourceReference) => importedSourceReference.HasOne<Artifact>().WithMany().HasForeignKey(x => x.ArtifactId).OnDelete(DeleteBehavior.Cascade);

    public void Configure(EntityTypeBuilder<ConversationKnowledge> conversationKnowledge) => conversationKnowledge.HasOne<Conversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);

    public void Configure(EntityTypeBuilder<KnowledgeCollection> knowledgeCollection) => knowledgeCollection.HasOne<WorkspaceResource>().WithMany().HasForeignKey(x => x.Id).OnDelete(DeleteBehavior.Restrict);

    public void Configure(EntityTypeBuilder<KnowledgeDocument> knowledgeDocument)
    {
        knowledgeDocument.HasOne<WorkspaceResource>().WithMany().HasForeignKey(x => x.Id).OnDelete(DeleteBehavior.Restrict);
        knowledgeDocument.HasOne<Attachment>().WithMany().HasForeignKey(x => x.AttachmentId).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<MessageCitation> messageCitation) => messageCitation.HasOne<Message>().WithMany().HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Cascade);

    public void Configure(EntityTypeBuilder<PromptTemplate> promptTemplate) => promptTemplate.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);

    public void Configure(EntityTypeBuilder<WorkspaceNotification> workspaceNotification) => workspaceNotification.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);

    public void Configure(EntityTypeBuilder<BackgroundJob> backgroundJob)
    {
        backgroundJob.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        backgroundJob.HasOne<WorkspaceResource>().WithMany().HasForeignKey(x => x.ResourceId).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<Project> project) => project.HasOne<WorkspaceResource>().WithMany().HasForeignKey(x => x.Id).OnDelete(DeleteBehavior.Restrict);

    public void Configure(EntityTypeBuilder<EvaluationResult> evaluationResult) => evaluationResult.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.ReviewerId).OnDelete(DeleteBehavior.Restrict);

    public void Configure(EntityTypeBuilder<EvaluationRun> evaluationRun)
    {
        evaluationRun.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        evaluationRun.HasOne<BackgroundJob>().WithMany().HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<EvaluationSet> evaluationSet) => evaluationSet.HasOne<WorkspaceResource>().WithMany().HasForeignKey(x => x.Id).OnDelete(DeleteBehavior.Restrict);

    public void Configure(EntityTypeBuilder<MessageFeedback> messageFeedback)
    {
        messageFeedback.HasOne<Message>().WithMany().HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Restrict);
        messageFeedback.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<RetrievalEvaluation> retrievalEvaluation)
    {
        retrievalEvaluation.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        retrievalEvaluation.HasOne<BackgroundJob>().WithMany().HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<RepositoryConnection> repositoryConnection) => repositoryConnection.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);

    public void Configure(EntityTypeBuilder<RepositoryImport> repositoryImport) => repositoryImport.HasOne<KnowledgeDocument>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);

    public void Configure(EntityTypeBuilder<RepositoryReview> repositoryReview)
    {
        repositoryReview.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        repositoryReview.HasOne<BackgroundJob>().WithMany().HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<ShareLink> shareLink)
    {
        shareLink.HasOne<WorkspaceResource>().WithMany().HasForeignKey(x => x.Id).OnDelete(DeleteBehavior.Restrict);
        shareLink.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<ShareRecipient> shareRecipient) => shareRecipient.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);

    public void Configure(EntityTypeBuilder<WebSearchRecord> webSearchRecord) => webSearchRecord.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);

    public void Configure(EntityTypeBuilder<GenerationRun> run)
    {
        run.HasOne<Conversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Restrict);
        run.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        run.HasOne<Message>().WithMany().HasForeignKey(x => x.UserMessageId).OnDelete(DeleteBehavior.Restrict);
        run.HasOne<Message>().WithMany().HasForeignKey(x => x.AssistantMessageId).OnDelete(DeleteBehavior.Restrict);
    }
}
