namespace AiNexus.Features.Staff.Attendance.Cards;

/// <summary>持有期間請求的共同欄位，讓新增與修改共用同一組格式規則。</summary>
public interface IAssignmentFields
{
    Guid? UserId { get; }
    string? HolderName { get; }
    DateTimeOffset StartsAt { get; }
    DateTimeOffset? EndsAt { get; }
    string? Note { get; }
}
