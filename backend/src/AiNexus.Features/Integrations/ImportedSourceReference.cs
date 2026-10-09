using AiNexus.Features.Artifacts;
using AiNexus.Features.Conversations;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Integrations;

/// <summary>Provenance of a private snapshot imported from a read-only source.</summary>
public sealed class ImportedSourceReference
{
    public Guid ArtifactId { get; set; }
    public string SourceId { get; set; } = "";
    public string ExternalId { get; set; } = "";
    public string Revision { get; set; } = "";
    public DateTimeOffset ImportedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed record SourceImportRequest(string RecordId, string ExpectedRevision);
public sealed record SourceChatDto(ConversationDto Conversation, string Prompt);

internal static class IntegrationErrors
{
    public static readonly Error UnknownSource = Error.NotFound("source_unknown");
    public static readonly Error FeatureRevoked = Error.Forbidden("integration_feature_revoked");
    public static readonly Error TransportUnsupported = Error.Unavailable("source_transport_unsupported");
    public static readonly Error NotConfigured = Error.Unavailable("source_not_configured");
    public static readonly Error SourceForbidden = Error.Forbidden("source_forbidden");
    public static readonly Error IdentityMissing = Error.Forbidden("source_identity_missing");
    public static readonly Error SearchInvalid = Error.Invalid("source_search_invalid");
    public static readonly Error RecordIdInvalid = Error.Invalid("source_id_invalid");
    public static readonly Error RecordMissing = Error.NotFound("source_record_missing");
    public static readonly Error ContractInvalid = new(ErrorKind.Upstream, "source_contract_invalid");
    public static readonly Error Changed = Error.Conflict("source_changed");
    public static readonly Error ArtifactFeatureRequired = Error.Forbidden("artifact_feature_required");
    public static readonly Error ChatFeatureRequired = Error.Forbidden("chat_feature_required");
    public static readonly Error ChatTooLong = Error.Conflict("source_chat_too_long");
}

internal sealed class ImportedSourceReferenceConfiguration : IEntityTypeConfiguration<ImportedSourceReference>
{
    public void Configure(EntityTypeBuilder<ImportedSourceReference> r)
    {
        r.ToTable("SourceReferences", "content"); r.HasKey(x => x.ArtifactId);
        r.Property(x => x.SourceId).HasMaxLength(32); r.Property(x => x.ExternalId).HasMaxLength(160); r.Property(x => x.Revision).HasMaxLength(160);
        r.HasIndex(x => new { x.SourceId, x.ExternalId });
    }
}
