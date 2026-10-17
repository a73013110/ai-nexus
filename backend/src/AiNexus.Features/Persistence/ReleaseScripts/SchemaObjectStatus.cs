namespace AiNexus.Features.Persistence.ReleaseScripts;

/// <summary>此次發布相對於正式環境，結構物件的變化方式。</summary>
public enum SchemaObjectStatus
{
    Added,
    Removed,
    Modified,
}
