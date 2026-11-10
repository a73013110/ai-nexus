using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Platform.Notifications;

public sealed class NotificationsOptions
{
    public const string Section = "Notifications";

    /// <summary>已讀或已移除的通知保存天數；超過的由 <see cref="NotificationRetentionTask"/> 刪除。下限 30 天，避免設錯值一次清光。</summary>
    [Range(30, 3650)] public int RetentionDays { get; set; } = 90;
}

[OptionsValidator]
public sealed partial class NotificationsOptionsValidator : IValidateOptions<NotificationsOptions>;
