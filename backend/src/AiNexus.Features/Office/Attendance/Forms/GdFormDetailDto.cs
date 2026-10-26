using AiNexus.Features.Platform.Gd;
using AiNexus.Features.Platform.RemoteFiles;

namespace AiNexus.Features.Office.Attendance.Forms;

/// <summary>
/// 一張公文系統表單的完整內容（唯讀），從出勤紀錄點單號開啟。時間都是 +08:00；<paramref name="Hours"/> 是 GD 的時數（8 小時算一天）。
/// <paramref name="Title"/> 是請假單的假別名稱或出差單的事由；<paramref name="Segments"/> 只有跨期間假單才有，是 <c>Form_Leave_Group</c> 的各段，
/// 此時 <paramref name="StartsAt"/>～<paramref name="EndsAt"/> 是整個範圍。
/// <paramref name="Steps"/>、<paramref name="Attachments"/> 為 null 表示公文系統的那部分讀不到（例如帳號還沒有流程表的授權），表單本身照常顯示。
/// </summary>
public sealed record GdFormDetailDto(
    string DesNo,
    GdFormKind Kind,
    GdFormFlowOver FlowStatus,
    string Title,
    DateTimeOffset? SubmittedAt,
    GdPerson? Applicant,
    GdPerson? Agent,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    double? Hours,
    IReadOnlyList<GdFormPeriod> Segments,
    string? Reason,
    string? AttachmentNote,
    GdLeaveDetail? Leave,
    GdTravelDetail? Travel,
    IReadOnlyList<GdFlowStep>? Steps,
    IReadOnlyList<GdFormAttachmentDto>? Attachments);

/// <summary>跨期間假單的一段 [<paramref name="StartsAt"/>, <paramref name="EndsAt"/>)。</summary>
public sealed record GdFormPeriod(DateTimeOffset StartsAt, DateTimeOffset EndsAt);

/// <summary>請假單才有的欄位：喪假的親屬關係（GD 的 <c>BereaveType</c>），以及申請人勾選要附的文件種類（<c>AttKind</c>）。</summary>
public sealed record GdLeaveDetail(string? Relation, IReadOnlyList<string> AttachmentKinds);

/// <summary>出差單才有的欄位：<paramref name="StudyKind"/> 有值表示是研習單（教師研習、行政人員研習）；<paramref name="Phone"/> 是申請人的分機。</summary>
public sealed record GdTravelDetail(string? Place, string? Transport, string? StudyKind, string? Phone);

/// <summary>表單的附件：<paramref name="File"/> 帶有短期憑證，瀏覽器憑它預覽或下載，看不到公文系統的網址。</summary>
public sealed record GdFormAttachmentDto(RemoteFileDto File, string? Memo, string? Uploader);
