using AiNexus.Platform.Data.External;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Platform.Gd;

/// <summary>
/// 公文系統 GD：資料庫定義（<see cref="GdDatabase"/>，存取與監控 <c>gd</c> 由 Platform 的 <see cref="ExternalDatabase{TDatabase}"/> 提供），
/// 以及多個模組共用的帳號對應 <see cref="GdAccounts"/>。查假單、部門等業務 SQL 寫在使用端（出勤、組織），不放這裡。
/// </summary>
public sealed class GdModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddExternalDatabase<GdDatabase>();
        builder.Services.AddScoped<GdAccounts>();
    }
}
