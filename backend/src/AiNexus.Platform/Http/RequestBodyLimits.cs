using AiNexus.Platform.Errors;
using Microsoft.AspNetCore.Http.Features;

namespace AiNexus.Platform.Http;

/// <summary>各端點的請求本體大小上限中繼資料，直接宣告於對應端點旁。</summary>
public sealed class RequestBodyLimitMetadata(Func<IServiceProvider, long> resolve)
{
    public long Resolve(IServiceProvider services) => resolve(services);
}

public static class RequestBodyLimits
{
    /// <summary>一般 JSON 請求的預設上限（64 KB）。</summary>
    public const long Default = 64 * 1024;

    /// <summary>將字元數換算為 JSON 位元組上限：考量 Unicode 跳脫字元（如 \uXXXX 佔 6 位元組），確保繁體中文等非 ASCII 文本在傳輸時不會因跳脫展開而誤觸長度限制。</summary>
    public static long ForJsonCharacters(int characters) => characters * 6L + 8192;

    public static TBuilder WithRequestBodyLimit<TBuilder>(this TBuilder builder, long bytes) where TBuilder : IEndpointConventionBuilder
        => builder.WithRequestBodyLimit(_ => bytes);

    public static TBuilder WithRequestBodyLimit<TBuilder>(this TBuilder builder, Func<IServiceProvider, long> resolve) where TBuilder : IEndpointConventionBuilder
        => builder.WithMetadata(new RequestBodyLimitMetadata(resolve));

    /// <summary>請求本體限制中介軟體：提早拒絕宣告過大的請求本體，並將串流讀取上限設定為該端點之配額。</summary>
    public static IApplicationBuilder UseRequestBodyLimits(this IApplicationBuilder app) => app.Use(async (http, next) =>
    {
        var limit = http.GetEndpoint()?.Metadata.GetMetadata<RequestBodyLimitMetadata>()?.Resolve(http.RequestServices) ?? Default;
        if (http.Request.ContentLength > limit)
        {
            await Problems.WriteAsync(http, StatusCodes.Status413PayloadTooLarge, "request_too_large");
            return;
        }
        var bodySize = http.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (bodySize is { IsReadOnly: false }) bodySize.MaxRequestBodySize = limit;
        await next(http);
    });
}
