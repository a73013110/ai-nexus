using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;

namespace AiNexus.Features.Platform.Account;

internal sealed class GetPersonalUsage(PersonalUsage usage)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/settings/usage", async (ICurrentUser user, GetPersonalUsage handler, CancellationToken ct) => TypedResults.Ok(await handler.HandleAsync(user.Id, ct)))
        .NotAudited("使用者自己的用量")
        .WithName("GetPersonalUsage");

    public Task<PersonalUsageDto> HandleAsync(Guid owner, CancellationToken ct) => usage.ForOwnerAsync(owner, ct);
}
