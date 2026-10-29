using System.Reflection;
using System.Text.Json;

namespace AiNexus.Features.Admin.Releases;

/// <summary>本版的一則更新：tag 之間 <c>feat</c>／<c>fix</c> commit 的主旨。</summary>
public sealed record ReleaseNoteDto(string Type, string Scope, string Subject);

/// <summary>
/// 執行中這一版程式的版本資訊，啟動時讀一次（請求路徑沒有成本）。版號與 commit 來自組件的 InformationalVersion
/// （<c>Build.ps1</c> 以 <c>-p:Version</c>、<c>-p:SourceRevisionId</c> 寫入）；建置時間、需要的最低資料庫版本與本版更新來自
/// <c>Release.ps1</c> 寫在內容根目錄的 <c>version.json</c>，只有發布包才有（ADR 0027）。
/// </summary>
public sealed class AppRelease
{
    public const string FileName = "version.json";

    public AppRelease(IHostEnvironment environment) : this(
        typeof(AppRelease).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0-dev",
        File.Exists(Path.Combine(environment.ContentRootPath, FileName)) ? File.ReadAllBytes(Path.Combine(environment.ContentRootPath, FileName)) : null)
    {
    }

    /// <param name="informationalVersion"><c>版號+commit</c>。</param>
    /// <param name="versionFile"><c>version.json</c> 的內容；沒有打包時為 null。</param>
    public AppRelease(string informationalVersion, byte[]? versionFile)
    {
        var plus = informationalVersion.IndexOf('+', StringComparison.Ordinal);
        Version = plus < 0 ? informationalVersion : informationalVersion[..plus];
        Revision = plus < 0 ? null : informationalVersion[(plus + 1)..];

        if (versionFile is null) return;
        // 檔案由打包工具產生；格式不對時當作沒有，不讓啟動失敗：缺的只是顯示用資訊與版號檢查。
        try
        {
            var file = JsonSerializer.Deserialize<VersionFile>(versionFile, JsonSerializerOptions.Web);
            Packaged = file is not null;
            BuiltAt = file?.BuiltAt;
            MinimumDatabaseVersion = file?.MinimumDatabaseVersion;
            Changes = [.. file?.Changes ?? []];
        }
        catch (JsonException)
        {
        }
    }

    /// <summary>版號（YYYY.MM.N，未發版的建置為 <c>0.0.0-dev</c> 或 <c>&lt;版號&gt;-dev.&lt;commit 數&gt;</c>）。</summary>
    public string Version { get; }

    /// <summary>建置的 commit；沒有帶 <c>SourceRevisionId</c> 的建置為 null。</summary>
    public string? Revision { get; }

    /// <summary>由 <c>Release.ps1</c> 打包（有 <c>version.json</c>）；只有這時才記錄程式的部署。</summary>
    public bool Packaged { get; }

    public DateTimeOffset? BuiltAt { get; }

    /// <summary>這版程式需要的最低資料庫版本（本版以前最近一個含資料庫的 tag）；未打包或還沒有資料庫發版時為 null，不檢查。</summary>
    public string? MinimumDatabaseVersion { get; }

    public IReadOnlyList<ReleaseNoteDto> Changes { get; } = [];

    private sealed record VersionFile(DateTimeOffset? BuiltAt, string? MinimumDatabaseVersion, List<ReleaseNoteDto>? Changes);
}
