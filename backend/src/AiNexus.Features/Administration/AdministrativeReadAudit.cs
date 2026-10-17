using System.Text.Json;
using AiNexus.Features.AccessControl;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Administration;

/// <summary>
/// 管理者唯讀存取使用者資料的稽核關卡。它不放寬對話擁有者檢查，且稽核寫入成功後才會回傳資料。
/// </summary>
internal sealed class AdministrativeReadAudit(NexusDbContext db, AccessService access)
{
    public async Task<bool> AllowedAsync(Guid actor, CancellationToken ct)
        => (await access.ForUserAsync(actor, ct)).Features.Any(x => x.Id == AdministrationConfiguration.Feature);

    public async Task<Result> RecordAsync(Guid actor, string action, Guid resource, object details, CancellationToken ct)
    {
        if (!await AllowedAsync(actor, ct)) return AccessControlErrors.FeatureForbidden;
        db.AuditEvents.Add(new() { OwnerId = actor, ResourceId = resource, Action = action, Result = "read", DetailsJson = JsonSerializer.Serialize(details) });
        // 稽核寫入失敗時 SaveChanges 會拋例外，呼叫端因此拿不到資料，等同失敗即拒絕。
        await db.SaveChangesAsync(ct);
        return Result.Success;
    }

    public static bool ValidPage(string? search, int offset) => !(search?.Length > 120 || offset < 0);
}
