using AiNexus.Features.Admin.Organization;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Office.Flow.Rules;

/// <summary>
/// <c>applicant-manager</c>：申請人往上第 N 層主管（<see cref="OrganizationDirectory.ManagerChainAsync"/>，含首長單位）。
/// 沒有主管的單位不算一層；層數超過頂端時取最上層的主管，到頂都沒有才交給簽核管理員。
/// </summary>
internal sealed class ApplicantManagerRule(OrganizationDirectory directory) : IApproverRule
{
    public const int MaxLevels = 5;

    public static readonly ApproverRuleDefinition Definition = new("applicant-manager", "申請人的主管",
        "申請人主要所屬單位往上的主管；1＝直屬主管、2＝再上一層。沒有主管的單位會跳過，往上找到頂為止。", ApproverArguments.Levels);

    public Task<Error?> ValidateAsync(string? argument, CancellationToken ct)
        => Task.FromResult<Error?>(Levels(argument) is null ? FlowErrors.ArgumentInvalid : null);

    public async Task<IReadOnlyCollection<Guid>> ResolveAsync(ApproverContext context, CancellationToken ct)
    {
        if (Levels(context.Argument) is not { } levels) return [];
        var chain = await directory.ManagerChainAsync(context.ApplicantId, ct);
        return chain.Count == 0 ? [] : [.. chain[Math.Min(levels, chain.Count) - 1]];
    }

    private static int? Levels(string? argument)
        => ApproverArgument.Read<LevelsArgument>(argument)?.Levels is { } levels and >= 1 and <= MaxLevels ? levels : null;

    private sealed record LevelsArgument(int Levels);
}
