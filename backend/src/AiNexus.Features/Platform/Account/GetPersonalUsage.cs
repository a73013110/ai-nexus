using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Platform.Account;

internal sealed class GetPersonalUsage(PersonalUsage usage) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapGet("/settings/usage", (ICurrentUser user, GetPersonalUsage handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, ct))
        .NotAudited("使用者自己的用量")
        .WithName("GetPersonalUsage");

    public Task<PersonalUsageDto> HandleAsync(Guid owner, CancellationToken ct) => usage.ForOwnerAsync(owner, ct);
}
