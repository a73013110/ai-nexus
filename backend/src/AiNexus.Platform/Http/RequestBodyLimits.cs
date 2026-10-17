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

    /// <summary>
    /// 把字元數換算成 JSON 位元組上限。Unicode 跳脫（如 \uXXXX 佔 6 個位元組）會讓非 ASCII 文字膨脹，
    /// 因此每字元以 6 位元組計，另加 8 KB 的結構餘裕，避免繁體中文等內容被誤擋。
    /// </summary>
    public static long ForJsonCharacters(int characters) => characters * 6L + 8192;

    public static TBuilder WithRequestBodyLimit<TBuilder>(this TBuilder builder, long bytes) where TBuilder : IEndpointConventionBuilder
        => builder.WithRequestBodyLimit(_ => bytes);

    public static TBuilder WithRequestBodyLimit<TBuilder>(this TBuilder builder, Func<IServiceProvider, long> resolve) where TBuilder : IEndpointConventionBuilder
        => builder.WithMetadata(new RequestBodyLimitMetadata(resolve));

    /// <summary>請求本體限制的中介軟體：提早拒絕宣告過大的請求本體，並把串流讀取上限設為該端點的配額。</summary>
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
