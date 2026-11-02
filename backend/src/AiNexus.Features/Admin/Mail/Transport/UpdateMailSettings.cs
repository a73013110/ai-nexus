using AiNexus.Data.App;
using AiNexus.Data.App.Mail;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Mail.Transport;

/// <summary>管理頁可切換的寄送設定；目前只有自動重試。</summary>
[ValidatedInHandler("Only a boolean; there is nothing to validate beyond JSON binding.")]
public sealed record MailSettingsRequest(bool AutoRetry);

/// <summary>儲存後的設定；<paramref name="UpdatedAt"/> 為 null 表示從未調整、使用預設值。</summary>
public sealed record MailSettingsDto(bool AutoRetry, DateTimeOffset? UpdatedAt);

/// <summary>
/// 切換自動重試（<c>mail.DeliverySettings</c>，下一輪寄送就生效、不必重啟）；寫行政稽核 <c>admin.mail_settings</c>。
/// 關閉後暫時性與傳輸層失敗第一次就結束，已在等待重試的信不受影響，仍可立即重試或重寄。權限與重寄相同（<c>mail.messages.resend</c>）。
/// </summary>
internal sealed class UpdateMailSettings(AppDbContext db, AdministrativeWrite write, TimeProvider clock)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPut("", (MailSettingsRequest body, IRequestActor actor, UpdateMailSettings handler, CancellationToken ct) =>
            handler.HandleAsync(actor.Id, body, ct))
        .RequireFeature(FeatureCatalog.MailMessagesResend)
        .Audited(MailAudit.SettingsUpdated)
        .WithName("UpdateMailSettings");

    public async Task<Result<MailSettingsDto>> HandleAsync(Guid actor, MailSettingsRequest request, CancellationToken ct)
    {
        // 單一設定、後寫的贏，前後值都在稽核；第一次新增撞上主鍵時回寫入衝突。
        var begun = await write.BeginAsync(AuditTarget.Of("mail.settings", async token =>
            await db.DeliverySettings.AsNoTracking().Where(x => x.Id == DeliverySetting.SingletonId).Select(x => new { autoRetry = x.AutoRetry }).SingleOrDefaultAsync(token)), ct);
        if (!begun.IsSuccess) return begun.Error;
        await using var transaction = begun.Value;

        var setting = await db.DeliverySettings.SingleOrDefaultAsync(x => x.Id == DeliverySetting.SingletonId, ct);
        if (setting is null) db.Add(setting = new DeliverySetting());
        setting.AutoRetry = request.AutoRetry;
        setting.UpdatedAt = clock.GetUtcNow();
        setting.UpdatedById = actor;

        var committed = await transaction.CommitAsync(ct);
        return committed.IsSuccess ? new MailSettingsDto(request.AutoRetry, setting.UpdatedAt) : committed.Error;
    }
}
