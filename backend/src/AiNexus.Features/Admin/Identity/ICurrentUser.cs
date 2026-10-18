using AiNexus.Data.App.Identity;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Admin.Identity;

/// <summary>
/// 目前登入的使用者，在 handler 執行時已解析完成。宣告 <see cref="ICurrentUser"/> 參數的端點會由 <see cref="CurrentUserResolution"/> 先解析一次，
/// handler 直接同步讀取 <see cref="Id"/>，不必在每個呼叫處各自 await 查詢。
/// </summary>
public interface ICurrentUser
{
    Guid Id { get; }
    User User { get; }
}

public static class CurrentUserResolution
{
    /// <summary>在有 <see cref="ICurrentUser"/> 或 <see cref="IRequestActor"/> 參數的 handler 執行前解析使用者；其他端點不受影響。</summary>
    public static RouteGroupBuilder WithCurrentUser(this RouteGroupBuilder group) => group.AddEndpointFilterFactory((context, next) =>
    {
        // 沒有宣告 ICurrentUser／IRequestActor 的端點直接略過，不產生任何解析成本。
        // IRequestActor 給不能依賴 Identity 的 AccessControl 端點使用，解析的是同一個使用者。
        if (!context.MethodInfo.GetParameters().Any(parameter => parameter.ParameterType == typeof(ICurrentUser) || parameter.ParameterType == typeof(IRequestActor))) return next;
        return async invocation =>
        {
            var user = await invocation.HttpContext.RequestServices.GetRequiredService<CurrentUser>().GetAsync(invocation.HttpContext.RequestAborted);
            return user.IsSuccess ? await next(invocation) : user.Error.ToProblem();
        };
    });
}
