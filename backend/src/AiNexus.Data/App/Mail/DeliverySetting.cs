using AiNexus.Data.App.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Mail;

/// <summary>
/// 郵件寄送的執行期設定（單列，<see cref="SingletonId"/>），由管理頁修改、不必重啟；沒有列時用預設值。
/// 傳輸、帳密與上限仍在 <c>Mail</c> 設定區段：那些要和秘密一起部署，這裡只放營運時會切換的開關。
/// </summary>
public sealed class DeliverySetting
{
    public const byte SingletonId = 1;

    public byte Id { get; set; } = SingletonId;
    public bool AutoRetry { get; set; } = true;
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid UpdatedById { get; set; }
}

internal sealed class DeliverySettingConfiguration : IEntityTypeConfiguration<DeliverySetting>
{
    public void Configure(EntityTypeBuilder<DeliverySetting> setting)
    {
        setting.ToTable("DeliverySettings", "mail");
        setting.HasKey(x => x.Id);
        setting.Property(x => x.Id).ValueGeneratedNever();
        setting.HasOne<User>().WithMany().HasForeignKey(x => x.UpdatedById).OnDelete(DeleteBehavior.Restrict);
    }
}
