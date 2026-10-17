using AiNexus.Platform.Errors;
using AiNexus.Features.Identity.Users;

namespace AiNexus.Features.Identity;

/// <summary>
/// 目前登入的使用者，在 handler 執行時已解析完成。宣告 <see cref="ICurrentUser"/> 參數的端點會由 <see cref="CurrentUserResolution"/> 先解析一次，
/// handler 直接同步讀取 <see cref="Id"/>，不必在每個呼叫處各自 await 查詢。
/// </summary>
public interface ICurrentUser
{
    Guid Id { get; }
    NexusUser User { get; }
}

public static class CurrentUserResolution
{
    /// <summary>在有 <see cref="ICurrentUser"/> 參數的 handler 執行前解析使用者；其他端點不受影響。</summary>
    public static RouteGroupBuilder WithCurrentUser(this RouteGroupBuilder group) => group.AddEndpointFilterFactory((context, next) =>
        !context.MethodInfo.GetParameters().Any(parameter => parameter.ParameterType == typeof(ICurrentUser)) ? next : async invocation =>
        {
            var user = await invocation.HttpContext.RequestServices.GetRequiredService<CurrentUser>().GetAsync(invocation.HttpContext.RequestAborted);
            return user.IsSuccess ? await next(invocation) : user.Error.ToProblem();
        });
}
