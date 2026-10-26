using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Configuration;
using AiNexus.Platform.Modules;
using AiNexus.Platform.RemoteFiles;

namespace AiNexus.Features.Platform.RemoteFiles;

/// <summary>
/// 別的系統上的檔案（目前是公文系統的附件）：使用案例以 <see cref="RemoteFileTickets"/> 發短期憑證，瀏覽器憑它從 <see cref="DownloadRemoteFile"/> 取內容，
/// 對方系統的網址留在後端。來源由系統模組以 <see cref="RemoteFileSources.AddRemoteFileSource{TSource}"/> 註冊（介面在 Platform，系統模組因此不依賴這裡）。
/// </summary>
public sealed class RemoteFilesModule : IFeatureModule
{
    /// <summary>每次下載都會打到對方系統，每人每分鐘 120 次（一張單的附件縮圖加預覽綽綽有餘）。</summary>
    public const string RateLimit = "remote-files";

    public static void AddServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddSettings<RemoteFilesOptions, RemoteFilesOptionsValidator>(RemoteFilesOptions.Section);
        builder.Services.AddSingleton<RemoteFileTickets>();
        builder.Services.AddRateLimiter(options => options.AddPerUserLimit(RateLimit, 120));
    }

    public static void MapEndpoints(RouteGroupBuilder api)
        => DownloadRemoteFile.Map(api.MapGroup("/remote-files").RequireRateLimiting(RateLimit));
}
