using AiNexus.Platform.Errors;

namespace AiNexus.Features.Admin.Integrations.Sources;

/// <summary>外部來源授權用的身分：AD SID 與完整帳號（<c>帳號@網域</c>，對應外部 view 的 <c>ActorSid</c>、<c>ActorAccount</c>）。</summary>
public sealed record SourceActor(string Sid, string Account);

public sealed record SourceSearchRequest(string Query, string Kind = "all");

public interface IControlledSourceAdapter
{
    string Id { get; }
    Task<IReadOnlyList<SourceRecordDto>> SearchAsync(SourceActor actor, SourceSearchRequest request, int take, int timeout, CancellationToken ct);
    /// <summary>回傳紀錄；使用者無權讀取或紀錄不存在時一律回傳 <c>source_record_missing</c>，不區分兩者，避免洩漏存在與否。</summary>
    Task<Result<SourceDetailDto>> ReadAsync(SourceActor actor, string id, int timeout, CancellationToken ct);
}
