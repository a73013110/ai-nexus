using AiNexus.Features.Identity.Users;

namespace AiNexus.Features.Identity;

/// <summary>
/// The signed-in user, already resolved when an endpoint handler runs. Endpoints that declare an
/// <see cref="ICurrentUser"/> parameter get it resolved once by <see cref="CurrentUserResolution"/>; handlers read
/// <see cref="Id"/> synchronously instead of awaiting a lookup at every call site.
/// </summary>
public interface ICurrentUser
{
    Guid Id { get; }
    NexusUser User { get; }
}

public static class CurrentUserResolution
{
    /// <summary>Resolves the user before handlers that take <see cref="ICurrentUser"/>; other endpoints are untouched.</summary>
    public static RouteGroupBuilder WithCurrentUser(this RouteGroupBuilder group) => group.AddEndpointFilterFactory((context, next) =>
        !context.MethodInfo.GetParameters().Any(parameter => parameter.ParameterType == typeof(ICurrentUser)) ? next : async invocation =>
        {
            await invocation.HttpContext.RequestServices.GetRequiredService<CurrentUser>().GetAsync(invocation.HttpContext.RequestAborted);
            return await next(invocation);
        });
}
