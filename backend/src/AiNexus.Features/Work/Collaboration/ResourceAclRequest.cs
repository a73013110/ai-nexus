using AiNexus.Platform.Validation;
using FluentValidation;

namespace AiNexus.Features.Work.Collaboration;

public sealed record ResourceAclRequest(IReadOnlyList<ResourceMemberUpdate> Members, IReadOnlyList<string> GroupIds);

public sealed record ResourceMemberUpdate(Guid UserId, string Role);

/// <summary>
/// 存取清單的形狀檢查，所有模組的存取端點共用。需查資料庫的部分（帳號是否存在、群組是否啟用、隱含的擁有者）
/// 留在 <see cref="ResourceAccess.SetAclAsync"/>。
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
