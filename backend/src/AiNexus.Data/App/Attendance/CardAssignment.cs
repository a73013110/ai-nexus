using AiNexus.Data.App.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Attendance;

/// <summary>
/// 卡號持有期間：刷卡時間落在 [<see cref="StartsAt"/>, <see cref="EndsAt"/>) 就歸給該持有人。
/// 同一張卡的期間不可重疊，由程式保證，資料庫不擋；修正對應只改這裡，刷卡原始資料不動。
/// </summary>
public sealed class CardAssignment
{
    public const int HolderNameMaxLength = 120, NoteMaxLength = 200;

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CardId { get; set; }

    /// <summary>空值表示持有人還沒有帳號（新進員工），此時須填 <see cref="HolderName"/>；帳號建立後綁上即可讓先前的刷卡歸戶。</summary>
    public Guid? UserId { get; set; }

    public string? HolderName { get; set; }
    public DateTimeOffset StartsAt { get; set; }

    /// <summary>不含；空值表示持有中。</summary>
    public DateTimeOffset? EndsAt { get; set; }

    public string? Note { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

internal sealed class CardAssignmentConfiguration : IEntityTypeConfiguration<CardAssignment>
{
    public void Configure(EntityTypeBuilder<CardAssignment> assignment)
    {
        assignment.Property(x => x.CreatedAt).HasValueGenerator<CreationTime>();
        assignment.ToTable("CardAssignments", "attendance");
        assignment.HasKey(x => x.Id);
        assignment.Property(x => x.HolderName).HasMaxLength(CardAssignment.HolderNameMaxLength);
        assignment.Property(x => x.Note).HasMaxLength(CardAssignment.NoteMaxLength);
        assignment.HasOne<Card>().WithMany().HasForeignKey(x => x.CardId).OnDelete(DeleteBehavior.Restrict);
        assignment.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
