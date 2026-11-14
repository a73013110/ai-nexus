using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Office.Flow.Rules;

/// <summary><c>users</c>：指定的人員任一人審核。儲存時人員都必須是啟用中的帳號；之後停用的由引擎排除，都停用時交給簽核管理員。</summary>
internal sealed class UsersRule(IActiveUsers activeUsers) : IApproverRule
{
    public const int MaxUsers = 20;

    public static readonly ApproverRuleDefinition Definition = new("users", "指定人員",
        $"指定的人員，任一人審核。\n最多 {MaxUsers} 位。", ApproverArguments.Users);

    public async Task<Error?> ValidateAsync(string? argument, CancellationToken ct)
    {
        if (Users(argument) is not { } users) return FlowErrors.ArgumentInvalid;
        return await activeUsers.Ids.CountAsync(x => users.Contains(x), ct) == users.Length ? null : FlowErrors.ArgumentInvalid;
    }

    public Task<IReadOnlyCollection<Guid>> ResolveAsync(ApproverContext context, CancellationToken ct)
        => Task.FromResult<IReadOnlyCollection<Guid>>(Users(context.Argument) ?? []);

    private static Guid[]? Users(string? argument)
        => ApproverArgument.UserIds(argument) is { Length: > 0 and <= MaxUsers } ids && ids.Distinct().Count() == ids.Length && !ids.Contains(Guid.Empty) ? ids : null;
}
