using AiNexus.Platform.Auditing;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Admin.Releases.Versions;

/// <summary>「關於」對話框：執行中這一版的版號、發布日期與本版更新。所有登入者可讀，不含資料庫資訊。</summary>
public sealed record CurrentReleaseDto(string Version, string? Revision, DateTimeOffset? BuiltAt, IReadOnlyList<ReleaseNoteDto> Changes);

internal sealed class GetCurrentRelease(AppRelease release) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapGet("/releases/current", (GetCurrentRelease handler) => handler.Handle())
        .NotAudited("目前版本與更新說明，所有使用者都看得到")
        .WithName("GetCurrentRelease");

    public CurrentReleaseDto Handle() => new(release.Version, release.Revision, release.BuiltAt, release.Changes);
}
