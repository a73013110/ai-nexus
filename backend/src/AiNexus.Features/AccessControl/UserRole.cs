namespace AiNexus.Features.AccessControl;

public sealed class UserRole
{
    public Guid UserId { get; set; }
    public string RoleId { get; set; } = "";
}
