using AiNexus.Platform.Configuration;
using AiNexus.Platform.Data.External;
using AiNexus.Platform.Modules;
using AiNexus.Platform.RemoteFiles;

namespace AiNexus.Features.Platform.Gd;

/// <summary>
/// 公文系統 GD：資料庫定義（<see cref="GdDatabase"/>，存取與監控 <c>gd</c> 由 Platform 的 <see cref="ExternalDatabase{TDatabase}"/> 提供），
/// 以及多個模組共用的 GD 知識：帳號對應 <see cref="GdAccounts"/>、人員顯示 <see cref="GdPeople"/>、簽核流程 <see cref="GdFlowReader"/>、
/// 附件 <see cref="GdAttachments"/> 與取回附件內容的 <see cref="GdFileSource"/>（網站設定在 <see cref="GdOptions"/>）。
/// 查假單、部門等業務 SQL 寫在使用端（出勤、組織），不放這裡。
/// </summary>
public sealed class GdModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddExternalDatabase<GdDatabase>();
        builder.Services.AddSettings<GdOptions, GdOptionsValidator>(GdOptions.Section);
        builder.Services.AddScoped<GdAccounts>();
        builder.Services.AddScoped<GdFlowReader>();
        builder.Services.AddScoped<GdAttachments>();
        builder.Services.AddRemoteFileSource<GdFileSource>();
    }
}
