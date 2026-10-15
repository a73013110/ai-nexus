namespace AiNexus.Features.Persistence.ReleaseScripts;

/// <summary>How a schema object differs between the production version and the release version.</summary>
public enum SchemaObjectStatus
{
    Added,
    Removed,
    Modified,
}
