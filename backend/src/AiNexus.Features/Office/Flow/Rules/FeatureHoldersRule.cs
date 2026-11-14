using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Office.Flow.Rules;

/// <summary><c>feature-holders</c>：目前持有指定功能（頁面或動作）的人任一人審核，例如由持有「使用者」管理頁的人審。</summary>
internal sealed class FeatureHoldersRule(AccessService access) : IApproverRule
{
    public static readonly ApproverRuleDefinition Definition = new("feature-holders", "持有功能的人",
        "目前持有指定功能（頁面或動作）的人任一人審核；授權異動後自動改變審核人。", ApproverArguments.Feature);

    public Task<Error?> ValidateAsync(string? argument, CancellationToken ct)
        => Task.FromResult<Error?>(Feature(argument) is null ? FlowErrors.ArgumentInvalid : null);

    public async Task<IReadOnlyCollection<Guid>> ResolveAsync(ApproverContext context, CancellationToken ct)
        => Feature(context.Argument) is { } feature ? await access.HoldersAsync(feature, ct) : [];

    private static string? Feature(string? argument)
        => ApproverArgument.Read<FeatureArgument>(argument)?.Feature is { } id && FeatureCatalog.Find(id) is { IsGrant: true } ? id : null;

    private sealed record FeatureArgument(string? Feature);
}
