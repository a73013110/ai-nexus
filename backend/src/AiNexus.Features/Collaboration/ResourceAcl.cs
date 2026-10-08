using AiNexus.Platform.Validation;
using FluentValidation;

namespace AiNexus.Features.Collaboration;

public sealed record ResourceMemberDto(Guid UserId, string Account, string DisplayName, string Role);
public sealed record ResourceAclDto(IReadOnlyList<ResourceMemberDto> Members, IReadOnlyList<string> GroupIds);
public sealed record ResourceAclRequest(IReadOnlyList<ResourceMemberUpdate> Members, IReadOnlyList<string> GroupIds);
public sealed record ResourceMemberUpdate(Guid UserId, string Role);

/// <summary>
/// The access list's shape, for every module's access endpoint. Known accounts, enabled groups and the implicit owner
/// need the database and stay in <see cref="ResourceAccess.SetAclAsync"/>, which also repeats these rules for direct callers.
/// </summary>
internal sealed class ResourceAclRequestValidator : RequestValidator<ResourceAclRequest>
{
    public const int MaxMembers = 50, MaxGroups = 20;

    public override string ProblemCode => "invalid_resource_acl";

    public ResourceAclRequestValidator()
    {
        RuleFor(x => x.Members).Cascade(CascadeMode.Stop).NotNull()
            .Must(x => x.All(m => m is not null)).WithErrorCode("not_null")
            .Must(x => x.Count <= MaxMembers).WithErrorCode("too_many")
            .Must(x => x.Select(m => m.UserId).Distinct().Count() == x.Count).WithErrorCode("duplicate")
            .Must(x => x.All(m => m.Role is "viewer" or "editor")).WithErrorCode("unknown");
        RuleFor(x => x.GroupIds).Cascade(CascadeMode.Stop).NotNull()
            .Must(x => x.Count <= MaxGroups).WithErrorCode("too_many")
            .Must(x => x.Distinct().Count() == x.Count).WithErrorCode("duplicate");
    }
}
