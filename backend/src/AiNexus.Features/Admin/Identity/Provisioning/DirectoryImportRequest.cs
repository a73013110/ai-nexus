using AiNexus.Platform.Validation;
using FluentValidation;

namespace AiNexus.Features.Admin.Identity.Provisioning;

/// <summary>要匯入的 AD 人員（SID，最多 100 位）；<paramref name="IncludeDisabled"/> 為 true 時 AD 已停用的人也建立或綁定（例如尚未到職、帳號先停用）。</summary>
public sealed record DirectoryImportRequest(IReadOnlyList<string> Sids, bool IncludeDisabled = false);

/// <summary>預覽與匯入共用：數量、重複與 SID 格式。實際能否匯入（衝突、已刪除、AD 找不到）是逐人的結果，不是 400。</summary>
internal sealed class DirectoryImportRequestValidator : RequestValidator<DirectoryImportRequest>
{
    public DirectoryImportRequestValidator()
    {
        RuleFor(x => x.Sids).NotNull().WithErrorCode("required")
            .Must(x => x is { Count: > 0 and <= DirectoryProvisioning.MaxBatch }).WithErrorCode("count")
            .Must(x => x is null || x.Distinct(StringComparer.OrdinalIgnoreCase).Count() == x.Count).WithErrorCode("duplicate");
        RuleForEach(x => x.Sids).Must(DirectoryProvisioning.IsSid).WithErrorCode("format");
    }
}
