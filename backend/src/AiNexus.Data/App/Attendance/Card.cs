using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Attendance;

/// <summary>HR 登錄的門禁卡；持有人不在這裡，見 <see cref="CardAssignment"/>。</summary>
public sealed class Card
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>打卡檔裡的卡號原始字串，不補零、不轉數字；與 <see cref="Punch.CardNumber"/> 以相同字串比對。</summary>
    public string CardNumber { get; set; } = "";

    /// <summary><see cref="CardKinds"/> 之一。</summary>
    public string Kind { get; set; } = CardKinds.Employee;

    public string? Note { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>卡片種類；訪客卡的刷卡照存但不歸戶，也不算未歸戶。</summary>
public static class CardKinds
{
    public const string Employee = "employee";
    public const string Visitor = "visitor";
}

internal sealed class CardConfiguration : IEntityTypeConfiguration<Card>
{
    public void Configure(EntityTypeBuilder<Card> card)
    {
        card.Property(x => x.CreatedAt).HasValueGenerator<CreationTime>();
        card.ToTable("Cards", "attendance");
        card.HasKey(x => x.Id);
        card.Property(x => x.CardNumber).HasMaxLength(32);
        card.Property(x => x.Kind).HasMaxLength(16);
        card.Property(x => x.Note).HasMaxLength(200);
    }
}
