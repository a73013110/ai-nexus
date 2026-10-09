using AiNexus.Features.AccessControl;
using AiNexus.Features.Administration;
using AiNexus.Features.Artifacts;
using AiNexus.Features.Attachments;
using AiNexus.Features.Billing;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Conversations;
using AiNexus.Features.Identity;
using AiNexus.Features.Inference;
using AiNexus.Features.Integrations;
using AiNexus.Features.Library;
using AiNexus.Features.Notifications;
using AiNexus.Features.Projects;
using AiNexus.Features.Quality;
using AiNexus.Features.Repositories;
using AiNexus.Features.Sharing;
using AiNexus.Features.WebSearch;
using Microsoft.EntityFrameworkCore;
using AiNexus.Features.Jobs;
using AiNexus.Features.Knowledge.Collections;
using AiNexus.Features.Knowledge.Documents;
using AiNexus.Features.Knowledge.Retrieval;

namespace AiNexus.Features.Persistence;

/// <summary>
/// Foreign keys whose two ends live in different modules. A module's own entity configuration only references its own
/// entities, so the database constraints stay intact without creating code dependencies between modules.
/// </summary>
internal static class CrossModuleRelationships
{
    public static void Configure(ModelBuilder model)
    {
        var userRole = model.Entity<UserRole>();
        userRole.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        var administratorBootstrap = model.Entity<AdministratorBootstrap>();
        administratorBootstrap.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        var userModelPolicy = model.Entity<UserModelPolicy>();
        userModelPolicy.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        var artifact = model.Entity<Artifact>();
        artifact.HasOne<WorkspaceResource>().WithMany().HasForeignKey(x => x.Id).OnDelete(DeleteBehavior.Restrict);
        artifact.HasOne<Message>().WithMany().HasForeignKey(x => x.SourceMessageId).OnDelete(DeleteBehavior.Restrict);
        // An artifact may belong to a project; ResourceLifecycle detaches artifacts before a project is deleted.
        artifact.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        var artifactRevision = model.Entity<ArtifactRevision>();
        artifactRevision.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Restrict);
        var attachment = model.Entity<Attachment>();
        attachment.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        var attachmentReference = model.Entity<AttachmentReference>();
        attachmentReference.HasOne<WorkspaceResource>().WithMany().HasForeignKey(x => x.ResourceId).OnDelete(DeleteBehavior.Restrict);
        var messageAttachment = model.Entity<MessageAttachment>();
        messageAttachment.HasOne<Message>().WithMany().HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Restrict);
        var modelCharge = model.Entity<ModelCharge>();
        modelCharge.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        modelCharge.HasOne<Conversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Restrict);
        var modelPrice = model.Entity<ModelPrice>();
        modelPrice.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        var resourceGroup = model.Entity<ResourceGroup>();
        resourceGroup.HasOne<RoleGroup>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
        var resourceMember = model.Entity<ResourceMember>();
        resourceMember.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        var workspaceResource = model.Entity<WorkspaceResource>();
        workspaceResource.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        var conversation = model.Entity<Conversation>();
        conversation.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        conversation.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        var modelInvocation = model.Entity<ModelInvocation>();
        modelInvocation.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        var importedSourceReference = model.Entity<ImportedSourceReference>();
        importedSourceReference.HasOne<Artifact>().WithMany().HasForeignKey(x => x.ArtifactId).OnDelete(DeleteBehavior.Cascade);
        var conversationKnowledge = model.Entity<ConversationKnowledge>();
        conversationKnowledge.HasOne<Conversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
        var knowledgeCollection = model.Entity<KnowledgeCollection>();
        knowledgeCollection.HasOne<WorkspaceResource>().WithMany().HasForeignKey(x => x.Id).OnDelete(DeleteBehavior.Restrict);
        var knowledgeDocument = model.Entity<KnowledgeDocument>();
        knowledgeDocument.HasOne<WorkspaceResource>().WithMany().HasForeignKey(x => x.Id).OnDelete(DeleteBehavior.Restrict);
        knowledgeDocument.HasOne<Attachment>().WithMany().HasForeignKey(x => x.AttachmentId).OnDelete(DeleteBehavior.Restrict);
        var messageCitation = model.Entity<MessageCitation>();
        messageCitation.HasOne<Message>().WithMany().HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Cascade);
        var promptTemplate = model.Entity<PromptTemplate>();
        promptTemplate.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        var workspaceNotification = model.Entity<WorkspaceNotification>();
        workspaceNotification.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        var backgroundJob = model.Entity<BackgroundJob>();
        backgroundJob.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        backgroundJob.HasOne<WorkspaceResource>().WithMany().HasForeignKey(x => x.ResourceId).OnDelete(DeleteBehavior.Restrict);
        var project = model.Entity<Project>();
        project.HasOne<WorkspaceResource>().WithMany().HasForeignKey(x => x.Id).OnDelete(DeleteBehavior.Restrict);
        var evaluationResult = model.Entity<EvaluationResult>();
        evaluationResult.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.ReviewerId).OnDelete(DeleteBehavior.Restrict);
        var evaluationRun = model.Entity<EvaluationRun>();
        evaluationRun.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        evaluationRun.HasOne<BackgroundJob>().WithMany().HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Restrict);
        var evaluationSet = model.Entity<EvaluationSet>();
        evaluationSet.HasOne<WorkspaceResource>().WithMany().HasForeignKey(x => x.Id).OnDelete(DeleteBehavior.Restrict);
        var messageFeedback = model.Entity<MessageFeedback>();
        messageFeedback.HasOne<Message>().WithMany().HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Restrict);
        messageFeedback.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        var retrievalEvaluation = model.Entity<RetrievalEvaluation>();
        retrievalEvaluation.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        retrievalEvaluation.HasOne<BackgroundJob>().WithMany().HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Restrict);
        var repositoryConnection = model.Entity<RepositoryConnection>();
        repositoryConnection.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        var repositoryImport = model.Entity<RepositoryImport>();
        repositoryImport.HasOne<KnowledgeDocument>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
        var repositoryReview = model.Entity<RepositoryReview>();
        repositoryReview.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        repositoryReview.HasOne<BackgroundJob>().WithMany().HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Restrict);
        var shareLink = model.Entity<ShareLink>();
        shareLink.HasOne<WorkspaceResource>().WithMany().HasForeignKey(x => x.Id).OnDelete(DeleteBehavior.Restrict);
        shareLink.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        var shareRecipient = model.Entity<ShareRecipient>();
        shareRecipient.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        var webSearchRecord = model.Entity<WebSearchRecord>();
        webSearchRecord.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
    }
}
