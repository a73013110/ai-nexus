using AiNexus.Data.App;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Validation;

namespace AiNexus.Features;

/// <summary>
/// 業務功能模組的組合點。組件內的模組、端點切片、請求驗證器（<see cref="FeatureCompositionAttribute"/>、ADR 0029），
/// 以及類別自己宣告的服務（實作組合擴充點的介面、標了 <see cref="ScopedAttribute"/>／<see cref="SingletonAttribute"/>，ADR 0033）由組合產生器在編譯期列出，
/// 新增模組只要在 <c>&lt;Area&gt;/&lt;Module&gt;/&lt;Module&gt;Module.cs</c> 實作 <see cref="IFeatureModule"/>，不必改這個檔案。
/// 模組依完整型別名稱排序註冊與掛載；順序固定但沒有意義，模組之間不可依賴它。
/// </summary>
[FeatureComposition]
public static partial class FeatureModules
{
    /// <summary>註冊所有業務功能模組的服務，以及它們共同依賴的主資料庫。</summary>
    public static IHostApplicationBuilder AddFeatures(this IHostApplicationBuilder builder)
    {
        builder.AddAppDatabase();
        AddModules(builder);
        AddDeclaredServices(builder.Services);
        AddEndpointHandlers(builder.Services);
        AddRequestValidators(builder.Services);
        return builder;
    }

    /// <summary>將各業務模組的 API 端點掛到 <c>/api/v1</c>；每個模組的 OpenAPI tag 是模組名（類別名去掉 Module）。</summary>
    public static WebApplication MapFeatures(this WebApplication app)
    {
        // 登入由 fallback policy 預設要求，群組與端點都不必再宣告；只有功能 policy 與 AllowAnonymous 這類例外才寫在端點上。
        // 篩選器依此順序執行：解析目前使用者（身分驗證結果）、開始請求稽核，接著執行請求驗證（驗證失敗也留下稽核）。
        // 三者都在建置端點時判斷是否適用，不適用的端點不掛過濾器。
        var api = app.MapGroup("/api/v1")
            .WithCurrentUser()
            .WithAudit()
            .WithRequestValidation();
        MapModules(api);
        return app;
    }

    /// <summary>
    /// 容器建好後、對外服務前檢查請求驗證器（同一 request 型別只能有一個）與依鍵查詢的服務（同一鍵只能有一個），再執行各模組的 <see cref="IFeatureModule.VerifyStartup"/>。
    /// </summary>
    public static void VerifyStartup(IServiceProvider services)
    {
        // 端點在第一個請求時才建置；先建好查詢表，重複的驗證器在啟動時就失敗，不會等到請求才發現。
        _ = services.GetRequiredService<RequestValidators>();
        services.VerifyKeyedServices();
        VerifyModules(services);
    }

    // 以下由組合產生器實作（AiNexus.Generators/Composition）。
    private static partial void AddModules(IHostApplicationBuilder builder);

    // 在模組之後：模組自己註冊的同一型別優先（TryAdd）。
    private static partial void AddDeclaredServices(IServiceCollection services);

    private static partial void MapModules(RouteGroupBuilder api);

    private static partial void VerifyModules(IServiceProvider services);

    private static partial void AddEndpointHandlers(IServiceCollection services);

    private static partial void AddRequestValidators(IServiceCollection services);
}
