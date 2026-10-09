namespace AiNexus.Features.Identity.Users;

public sealed record UserAuthenticationDto(bool AdEnabled, bool LocalEnabled, string? AdAccount, string? LocalAccount, bool HasLocalPassword);
