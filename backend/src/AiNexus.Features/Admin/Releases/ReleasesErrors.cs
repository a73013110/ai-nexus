using AiNexus.Platform.Errors;

namespace AiNexus.Features.Admin.Releases;

/// <summary>版本與結構異動的預期失敗。</summary>
public static class ReleasesErrors
{
    /// <summary>應有與實際的結構都沒有這個物件：已被還原，或名稱不對。</summary>
    public static readonly Error SchemaObjectNotFound = Error.NotFound("schema_object_not_found");
}
