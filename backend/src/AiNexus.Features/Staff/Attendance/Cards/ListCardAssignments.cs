using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Staff.Attendance.Cards;

/// <summary>
/// 持有歷史，新到舊。<c>cardId</c> 查某張卡的歷任持有人，<c>userId</c> 查某人持有過的卡（同一人可同時持有多張）；
/// 至少要給一個，不開放整張表。只有姓名、尚未綁上帳號的持有期間不會出現在 <c>userId</c> 的結果裡。
/// </summary>
internal sealed class ListCardAssignments(AppDbContext db)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("", (Guid? cardId, Guid? userId, ListCardAssignments handler, CancellationToken ct) => handler.HandleAsync(cardId, userId, ct).ToHttpResultAsync())
        .WithName("ListAttendanceCardAssignments");

    public async Task<Result<IReadOnlyList<CardAssignmentDto>>> HandleAsync(Guid? cardId, Guid? userId, CancellationToken ct)
    {
        if (cardId is null && userId is null) return AttendanceErrors.FilterInvalid;
        var query = db.Set<CardAssignment>().AsNoTracking();
        if (cardId is { } card) query = query.Where(x => x.CardId == card);
        if (userId is { } user) query = query.Where(x => x.UserId == user);
        return Result<IReadOnlyList<CardAssignmentDto>>.Ok(await query.OrderByDescending(x => x.StartsAt).ThenByDescending(x => x.Id).ToDtos(db).ToListAsync(ct));
    }
}
