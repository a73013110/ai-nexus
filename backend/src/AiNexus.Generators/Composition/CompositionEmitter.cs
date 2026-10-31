using AiNexus.Generators.Common;

namespace AiNexus.Generators.Composition;

/// <summary>
/// 寫出組合目標的 partial 方法實作。方法簽章（名稱、參數名、private static）是與目標類別的約定，
/// 目標類別少宣告一個就會編譯失敗（CS0759），不會悄悄少註冊。
/// </summary>
internal static class CompositionEmitter
{
    public static string Emit(CompositionTarget target, FeatureComposition composition)
    {
        var writer = new SourceWriter();
        if (target.Namespace is { } ns) writer.Line().Line($"namespace {ns};");
        writer.Line().Open($"static partial class {target.Name}");

        writer.Open("private static partial void AddModules(global::Microsoft.Extensions.Hosting.IHostApplicationBuilder builder)");
        foreach (var module in Valid(composition.Modules, x => x.Problem)) writer.Line($"{module.Type}.AddServices(builder);");
        writer.Close().Line();

        // 每個模組掛在一個空前綴、帶模組名 tag 的群組：端點自己不加 tag，管理端點分散在各模組時才找得到。
        writer.Open("private static partial void MapModules(global::Microsoft.AspNetCore.Routing.RouteGroupBuilder api)");
        foreach (var module in Valid(composition.Modules, x => x.Problem).Where(x => x.MapsEndpoints))
            writer.Line($"{module.Type}.MapEndpoints(global::Microsoft.AspNetCore.Http.OpenApiRouteHandlerBuilderExtensions.WithTags("
                + $"global::Microsoft.AspNetCore.Builder.EndpointRouteBuilderExtensions.MapGroup(api, \"\"), \"{module.Tag}\"));");
        writer.Close().Line();

        writer.Open("private static partial void VerifyModules(global::System.IServiceProvider services)");
        foreach (var module in Valid(composition.Modules, x => x.Problem).Where(x => x.VerifiesStartup)) writer.Line($"{module.Type}.VerifyStartup(services);");
        writer.Close().Line();

        // TryAdd：模組若已自行註冊切片（例如改成 singleton），以模組的為準。
        writer.Open("private static partial void AddEndpointHandlers(global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)");
        foreach (var slice in Valid(composition.Slices, x => x.Problem))
            writer.Line($"global::Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddScoped<{slice.Type}>(services);");
        writer.Close().Line();

        // 一律 singleton：過濾器在建置端點時解析驗證器並跨請求共用（RequestValidation）。
        writer.Open("private static partial void AddRequestValidators(global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)");
        foreach (var validator in Valid(composition.Validators, x => x.Problem))
            foreach (var service in validator.Services)
                writer.Line($"global::Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<{service}, {validator.Type}>(services);");
        writer.Close();

        return writer.Close().ToString();
    }

    private static IEnumerable<T> Valid<T>(EquatableArray<T> items, Func<T, DiagnosticInfo?> problem) where T : IEquatable<T>
        => items.Where(x => problem(x) is null);
}
