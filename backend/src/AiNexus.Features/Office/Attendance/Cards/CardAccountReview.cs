using System.Globalization;
using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Accounts;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Office.Attendance.Cards;

/// <summary>
/// 卡號持有期間的帳號待辦（ADR 0019）。持有期間會因卡片回收、轉發給別人而結束，不能在停用時自動結束，也不能在復職時自動接回，所以只提示：
/// <list type="bullet">
/// <item>帳號已停用卻還有沒結束的持有期間：出勤紀錄會把離職者每天列為未刷卡，卡轉給新人時也得先結束這一段。</item>
/// <item>帳號啟用、持有過卡，現在卻沒有：出勤紀錄只列持卡人，這個人會從出勤紀錄消失（常見於復職）。最後一張卡在出勤紀錄隱藏的人不提示。</item>
/// </list>
/// </summary>
internal sealed class CardAccountReview(AppDbContext db, IActiveUsers activeUsers, TimeProvider clock) : IAccountReviewSource
{
    public const string Held = "attendance.card_held";
    public const string NoCard = "attendance.no_card";

    public async Task<AccountReviewReading> ReadAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var active = activeUsers.Ids;
        var assignments = db.Set<CardAssignment>().AsNoTracking();
        var held = await assignments.UnendedAt(now).Where(x => x.UserId != null && !active.Contains(x.UserId.Value)).ToDtos(db).ToListAsync(ct);
        var heldIds = held.Select(x => x.Id).ToArray();
        var lastPunches = await db.Set<AttributedPunch>().Where(x => x.AssignmentId != null && heldIds.Contains(x.AssignmentId.Value))
            .GroupBy(x => x.AssignmentId!.Value).Select(x => new { x.Key, Last = x.Max(y => y.PunchedAt) }).ToDictionaryAsync(x => x.Key, x => x.Last, ct);

        var unended = assignments.UnendedAt(now).Where(x => x.UserId != null).Select(x => x.UserId!.Value);
        var lost = await assignments.Where(x => x.UserId != null && active.Contains(x.UserId.Value) && !unended.Contains(x.UserId.Value))
            .GroupBy(x => x.UserId!.Value).Select(x => x.OrderByDescending(y => y.EndsAt).First().Id).ToListAsync(ct);
        var hidden = db.Set<Card>().Where(x => x.HiddenFromRecords).Select(x => x.Id);
        var last = await assignments.Where(x => lost.Contains(x.Id) && !hidden.Contains(x.CardId)).ToDtos(db).ToListAsync(ct);
        var cards = last.Select(x => x.CardId).ToArray();
        var holders = await assignments.Where(x => cards.Contains(x.CardId)).UnendedAt(now).ToDtos(db).ToListAsync(ct);

        return new([
            .. held.GroupBy(x => x.UserId!.Value).Select(x => new AccountReviewItem(x.Key, Held, "帳號已停用，仍持有出勤卡",
                string.Join("、", x.OrderBy(y => y.CardNumber, StringComparer.Ordinal).Select(Punched)) + "。結束時間請設在離職生效日。",
                FeatureCatalog.AttendanceCards, Search(x.First().CardNumber))),
            .. last.Select(x => new AccountReviewItem(x.UserId!.Value, NoCard, "沒有持有中的出勤卡，出勤紀錄不會列出",
                $"上次持有卡號 {x.CardNumber}，至 {Time(x.EndsAt!.Value)}；{Holder(x.CardId)}。", FeatureCatalog.AttendanceCards, Search(x.CardNumber))),
        ]);

        string Punched(CardAssignmentDto x) => lastPunches.TryGetValue(x.Id, out var at) ? $"卡號 {x.CardNumber}（最後刷卡 {Time(at)}）" : $"卡號 {x.CardNumber}（沒有刷卡紀錄）";

        string Holder(Guid card) => holders.FirstOrDefault(x => x.CardId == card) is { } holder
            ? $"這張卡目前由 {holder.UserDisplayName ?? holder.HolderName} 持有" : "這張卡目前沒有人持有";
    }

    private static string Search(string cardNumber) => "search=" + Uri.EscapeDataString(cardNumber);

    private static string Time(DateTimeOffset value) => TaipeiTime.Of(value).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
}
