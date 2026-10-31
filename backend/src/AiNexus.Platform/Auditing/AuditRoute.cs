using Microsoft.AspNetCore.Routing.Patterns;

namespace AiNexus.Platform.Auditing;

/// <summary>
/// 請求稽核從路由取預設值的參數名稱：<see cref="Resource"/> 是對象（<c>{id}</c>），<see cref="Subject"/> 是資料主體
/// （<c>{userId}</c>，或 <c>users/{id}</c> 的 <c>id</c>）。由路由樣式決定，建置端點時算一次，請求時不必再解析樣式。
/// </summary>
public readonly record struct AuditRoute(string? Resource, string? Subject)
{
    public static AuditRoute Of(RoutePattern? pattern)
    {
        if (pattern?.GetParameter("id") is null) return new(null, pattern?.GetParameter("userId") is null ? null : "userId");
        return new("id", pattern.GetParameter("userId") is not null ? "userId" : UserId(pattern) ? "id" : null);
    }

    // 管理別人帳號的端點（users/{id}）：路由的 id 就是資料主體，不必各自 Describe。
    private static bool UserId(RoutePattern pattern)
    {
        var segments = pattern.PathSegments;
        for (var i = 1; i < segments.Count; i++)
            if (segments[i].Parts is [RoutePatternParameterPart { Name: "id" }] && segments[i - 1].Parts is [RoutePatternLiteralPart { Content: "users" }])
                return true;
        return false;
    }
}
