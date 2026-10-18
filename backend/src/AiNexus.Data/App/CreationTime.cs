using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace AiNexus.Features.Persistence;

/// <summary>建立時間欄位的值產生器，時間來源為 <see cref="NexusDbContext.Clock"/>。</summary>
/// <remarks>
/// 只有時間欄位仍為預設值時 EF 才會呼叫此產生器，呼叫端事先指定的時間會被保留（供測試與資料複製使用）。
/// 時間必須走 Clock 而非 <c>DateTime.UtcNow</c>，測試才能控制時間。
/// </remarks>
public sealed class CreationTime : ValueGenerator<DateTimeOffset>
{
    public override bool GeneratesTemporaryValues => false;

    public override DateTimeOffset Next(EntityEntry entry) =>
        ((NexusDbContext)entry.Context).Clock.GetUtcNow();
}
