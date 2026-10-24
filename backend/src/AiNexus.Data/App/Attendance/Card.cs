using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Attendance;

/// <summary>
/// 門禁卡，一個卡號一列：打卡檔出現的卡號在匯入時自動建立，HR 也可以先建立還沒刷過的卡。持有人不在這裡，見 <see cref="CardAssignment"/>。
/// </summary>
public sealed class Card
{
    public const int CardNumberMaxLength = 32, NoteMaxLength = 200;

    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>打卡檔裡的卡號原始字串，不補零、不轉數字；與 <see cref="Punch.CardNumber"/> 以相同字串比對。建立後不改，否則既有刷卡會對不到。</summary>
    public string CardNumber { get; set; } = "";

    public string? Note { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// 在出勤紀錄隱藏（董事、外派、訪客卡、測試卡）：HR 看全公司時不列出這張卡的持有期間與刷卡，也不算待指派。
    /// 只是檢視篩選、不是權限，本人與主管仍依資料範圍看得到。
    /// </summary>
    public bool HiddenFromRecords { get; set; }
}

internal sealed class CardConfiguration : IEntityTypeConfiguration<Card>
{
    public void Configure(EntityTypeBuilder<Card> card)
    {
        card.Property(x => x.CreatedAt).HasValueGenerator<CreationTime>();
        card.ToTable("Cards", "attendance");
        card.HasKey(x => x.Id);
        card.Property(x => x.CardNumber).HasMaxLength(Card.CardNumberMaxLength);
        card.Property(x => x.Note).HasMaxLength(Card.NoteMaxLength);
    }
}
