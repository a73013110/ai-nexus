using AiNexus.Data.App;
using AiNexus.Data.App.Flow;

namespace AiNexus.Features.Office.Flow.Approvals;

public sealed record FlowPersonDto(Guid Id, string DisplayName);

/// <summary>清單的一列；<paramref name="CurrentStepName"/> 只有審核中的申請有值。</summary>
public sealed record FlowInstanceSummaryDto(Guid Id, string Kind, string KindName, string Title, FlowPersonDto Applicant, string Status,
    string? CurrentStepName, DateTimeOffset SubmittedAt, DateTimeOffset? CompletedAt);

public sealed record FlowInstancePageDto(IReadOnlyList<FlowInstanceSummaryDto> Items, bool HasMore);

/// <summary>清單共用的投影：申請人姓名與目前關卡名稱在同一個 SQL 取得，種類名稱由 <see cref="FlowCatalog"/> 在記憶體補上。</summary>
internal static class FlowInstanceViews
{
    /// <summary>以屬性初始化（不用建構子）投影：EF 才能在投影之後排序與分頁。</summary>
    public sealed record Row
    {
        public required Guid Id { get; init; }
        public required string Kind { get; init; }
        public required string Title { get; init; }
        public required Guid ApplicantId { get; init; }
        public required string ApplicantName { get; init; }
        public required string Status { get; init; }
        public string? CurrentStepName { get; init; }
        public required DateTimeOffset SubmittedAt { get; init; }
        public DateTimeOffset? CompletedAt { get; init; }
    }

    public static IQueryable<Row> Rows(AppDbContext db, IQueryable<FlowInstance> instances)
        => from x in instances
           join applicant in db.Users on x.ApplicantId equals applicant.Id
           select new Row
           {
               Id = x.Id, Kind = x.Kind, Title = x.Title, ApplicantId = x.ApplicantId, ApplicantName = applicant.DisplayName, Status = x.Status,
               CurrentStepName = db.FlowInstanceSteps.Where(s => s.InstanceId == x.Id && s.StepNumber == x.CurrentStep).Select(s => s.Name).FirstOrDefault(),
               SubmittedAt = x.SubmittedAt, CompletedAt = x.CompletedAt,
           };

    public static FlowInstanceSummaryDto Summary(this Row row, FlowCatalog catalog)
        => new(row.Id, row.Kind, catalog.KindName(row.Kind), row.Title, new(row.ApplicantId, row.ApplicantName), row.Status,
            row.CurrentStepName, row.SubmittedAt, row.CompletedAt);
}
