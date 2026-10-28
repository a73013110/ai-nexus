using AiNexus.Data.App;
using AiNexus.Features.Platform.Inference.Models;
using AiNexus.Platform.Data.Sql;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Export;

namespace AiNexus.Features.Admin.Billing.Reports;

/// <summary>經驗證的報表期間（起訖必須有先後，長度最多 366 天），並帶有檢視者的 UTC 位移，用來切分每日統計。</summary>
public sealed record SpendPeriod(DateTimeOffset From, DateTimeOffset Until, int Offset)
{
    public static Result<SpendPeriod> Create(DateTimeOffset? from, DateTimeOffset? until, int? offset, DateTimeOffset now)
    {
        var end = until ?? now;
        var start = from ?? end.AddDays(-30);
        var zone = offset ?? 480;
        if (start >= end || end - start > TimeSpan.FromDays(366) || zone is < -840 or > 840) return BillingErrors.InvalidSpendPeriod;
        return new SpendPeriod(start.ToUniversalTime(), end.ToUniversalTime(), zone);
    }
}

/// <summary>
/// 花費報表的查詢：期間報表在 <c>billing.usp_GetSpendReport</c>、單一對話在 <c>billing.usp_GetConversationSpend</c>，各一次往返。
/// procedure 只依 Provider、ModelId 回傳原始彙總；模型顯示名稱的隱藏與合併、是否列出使用者，以及對話找不到時回哪個錯誤，都在這裡決定。
/// </summary>
public sealed class SpendReports(ISqlDatabase<AppDbContext> sql, ModelPresentation presentation)
{
    public Task<SpendReportDto> ReportAsync(Guid? owner, SpendPeriod p, bool administrator, CancellationToken ct)
    {
        // 只有全平台報表列出各使用者；期間與時區位移由程式決定，procedure 不用資料庫時鐘。
        var arguments = new { OwnerId = owner, p.From, p.Until, p.Offset, IncludeUsers = administrator };
        return sql.QueryProcedureAsync("billing.usp_GetSpendReport", arguments, async results =>
        {
            var summary = await results.ReadSingleAsync<ReportSummary>();
            var totals = await results.ReadAsync<MoneyTotalDto>();
            var daily = await results.ReadAsync<SpendBucketDto>();
            var models = MergePresentedModels(await results.ReadAsync<ModelRow>(), administrator);
            var users = await results.ReadAsync<SpendUserDto>();
            return new SpendReportDto(p.From, p.Until, p.Offset, summary.Requests, summary.PendingCalls, summary.LegacyCalls,
                summary.InputTokens, summary.OutputTokens, totals, daily, models, users);
        }, cancellationToken: ct);
    }

    public async Task<Result<ConversationSpendDto>> ConversationAsync(Guid owner, Guid id, CancellationToken ct)
    {
        var arguments = new { OwnerId = owner, ConversationId = id };
        var spend = await sql.QueryProcedureAsync<ConversationSpendDto?>("billing.usp_GetConversationSpend", arguments, async results =>
        {
            // 沒有摘要列＝對話不存在、已刪除或不是本人的；procedure 這時不再輸出其他結果集。
            if (await results.ReadAsync<ConversationSummary>() is not [var summary]) return null;
            var totals = await results.ReadAsync<MoneyTotalDto>();
            var models = MergePresentedModels(await results.ReadAsync<ModelRow>(), false);
            return new ConversationSpendDto(summary.Requests, summary.PendingCalls, summary.LegacyCalls, totals, models);
        }, cancellationToken: ct);
        return spend is null ? BillingErrors.ConversationNotFound : spend;
    }

    private string Label(string provider, string model, bool administrator) => presentation.DisplayName(model, administrator, provider)!;

    // 隱藏的模型名稱可能對應多個真實模型，它們會被合併到同一個公開標籤。
    private IReadOnlyList<SpendBucketDto> MergePresentedModels(IEnumerable<ModelRow> rows, bool administrator) => rows
        .GroupBy(x => new { Label = Label(x.Provider, x.ModelId, administrator), x.Currency, x.Kind })
        .Select(g => new SpendBucketDto(
            g.Key.Label,
            g.Key.Currency,
            g.Key.Kind,
            g.Sum(x => x.Amount),
            g.Sum(x => x.Requests),
            g.Sum(x => x.UnknownCalls),
            g.Sum(x => x.InputTokens),
            g.Sum(x => x.OutputTokens)))
        .OrderByDescending(x => x.Requests)
        .ToArray();

    public static ExportFile Export(SpendReportDto report) => new("ai-nexus-spend.csv", Csv.ContentType, Csv.Write(
        ["使用者", "帳號", "幣別", "計費類型", "已知金額", "呼叫次數", "未知費用次數"],
        report.Users.Select(user => new object?[] { user.DisplayName, user.Account, user.Currency, user.Kind, user.Amount, user.Requests, user.UnknownCalls })));

    // 以下 record 的欄位名稱、順序與型別與 procedure 的 SELECT 一致，只在執行時對應（整合測試涵蓋）；
    // 合計、每日與各使用者直接對應到 MoneyTotalDto、SpendBucketDto、SpendUserDto。
    private sealed record ReportSummary(int Requests, int PendingCalls, int LegacyCalls, long InputTokens, long OutputTokens);

    private sealed record ConversationSummary(int Requests, int PendingCalls, int LegacyCalls);

    private sealed record ModelRow(string Provider, string ModelId, string Currency, string Kind, decimal Amount, int Requests,
        int UnknownCalls, long InputTokens, long OutputTokens);
}
