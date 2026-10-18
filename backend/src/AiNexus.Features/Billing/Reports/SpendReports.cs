using AiNexus.Data.App;
using AiNexus.Data.App.Billing;
using AiNexus.Data.App.Inference;
using System.Globalization;
using System.Text;
using AiNexus.Features.Inference.Models;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Security;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Billing.Reports;

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

public sealed class SpendReports(AppDbContext db, ModelPresentation presentation)
{
    public async Task<SpendReportDto> ReportAsync(Guid? owner, SpendPeriod p, bool administrator, CancellationToken ct)
    {
        var query = db.Set<ModelCharge>().AsNoTracking().Where(x => x.CreatedAt >= p.From && x.CreatedAt < p.Until);
        if (owner is Guid id) query = query.Where(x => x.OwnerId == id);

        IReadOnlyList<SpendUserDto> users = [];
        var requests = await query.CountAsync(ct);
        var pending = await query.CountAsync(x => x.State == "pending", ct);
        var totals = await query.GroupBy(x => new { x.Currency, x.Kind }).Select(g => new MoneyTotalDto(
            g.Key.Currency,
            g.Key.Kind,
            g.Sum(x => x.Amount ?? 0),
            g.Count(x => x.Amount != null),
            g.Count(x => x.Amount == null && x.State != "pending"))).ToListAsync(ct);
        var daily = (await query
                .GroupBy(x => new { Day = x.CreatedAt.AddMinutes(p.Offset).Date, x.Currency, x.Kind })
                .Select(g => new
                {
                    g.Key.Day,
                    g.Key.Currency,
                    g.Key.Kind,
                    Amount = g.Sum(x => x.Amount ?? 0),
                    Requests = g.Count(),
                    Unknown = g.Count(x => x.Amount == null && x.State != "pending"),
                    Input = g.Sum(x => x.InputTokens ?? 0),
                    Output = g.Sum(x => x.OutputTokens ?? 0),
                })
                .OrderBy(x => x.Day)
                .ToListAsync(ct))
            .Select(x => new SpendBucketDto(
                x.Day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                x.Currency,
                x.Kind,
                x.Amount,
                x.Requests,
                x.Unknown,
                x.Input,
                x.Output))
            .ToArray();
        var models = (await query
                .GroupBy(x => new { x.Provider, x.ModelId, x.Currency, x.Kind })
                .Select(g => new
                {
                    g.Key.Provider,
                    g.Key.ModelId,
                    g.Key.Currency,
                    g.Key.Kind,
                    Amount = g.Sum(x => x.Amount ?? 0),
                    Requests = g.Count(),
                    Unknown = g.Count(x => x.Amount == null && x.State != "pending"),
                    Input = g.Sum(x => x.InputTokens ?? 0),
                    Output = g.Sum(x => x.OutputTokens ?? 0),
                })
                .OrderByDescending(x => x.Requests)
                .ToListAsync(ct))
            .Select(x => new SpendBucketDto(
                Label(x.Provider, x.ModelId, administrator),
                x.Currency,
                x.Kind,
                x.Amount,
                x.Requests,
                x.Unknown,
                x.Input,
                x.Output))
            .ToArray();
        var input = await query.SumAsync(x => x.InputTokens ?? 0, ct);
        var output = await query.SumAsync(x => x.OutputTokens ?? 0, ct);
        if (administrator)
        {
            users = await (from g in query.GroupBy(x => new { x.OwnerId, x.Currency, x.Kind })
                    select new
                    {
                        g.Key.OwnerId,
                        g.Key.Currency,
                        g.Key.Kind,
                        Amount = g.Sum(x => x.Amount ?? 0),
                        Requests = g.Count(),
                        Unknown = g.Count(x => x.Amount == null && x.State != "pending"),
                    })
                .Join(db.Users, x => x.OwnerId, u => u.Id, (x, u) => new
                {
                    u.Id,
                    u.DisplayName,
                    u.Account,
                    x.Currency,
                    x.Kind,
                    x.Amount,
                    x.Requests,
                    x.Unknown,
                })
                .OrderByDescending(x => x.Requests)
                .Select(x => new SpendUserDto(
                    x.Id,
                    x.DisplayName,
                    x.Account,
                    x.Currency,
                    x.Kind,
                    x.Amount,
                    x.Requests,
                    x.Unknown))
                .ToListAsync(ct);
        }

        // 舊版執行紀錄沒有費用紀錄，只計入呼叫次數，費用保持未知。
        var legacy = await db.Runs
                .CountAsync(x => (owner == null || x.OwnerId == owner) && x.CreatedAt >= p.From && x.CreatedAt < p.Until
                    && !db.Set<ModelCharge>().Any(c => c.Id == x.Id), ct)
            + await db.Set<ModelInvocation>()
                .CountAsync(x => (owner == null || x.OwnerId == owner) && x.CreatedAt >= p.From && x.CreatedAt < p.Until
                    && !db.Set<ModelCharge>().Any(c => c.Id == x.Id), ct);
        return new(p.From, p.Until, p.Offset, requests + legacy, pending, legacy, input, output, totals, daily, MergePresentedModels(models), users);
    }

    public async Task<Result<ConversationSpendDto>> ConversationAsync(Guid owner, Guid id, CancellationToken ct)
    {
        if (!await db.Conversations.AnyAsync(x => x.Id == id && x.OwnerId == owner, ct)) return BillingErrors.ConversationNotFound;
        var calls = db.Set<ModelCharge>().AsNoTracking().Where(x => x.OwnerId == owner && x.ConversationId == id);
        var legacy = await db.Runs.CountAsync(x => x.ConversationId == id && !db.Set<ModelCharge>().Any(c => c.Id == x.Id), ct);
        var count = await calls.CountAsync(ct);
        var pending = await calls.CountAsync(x => x.State == "pending", ct);
        var totals = await calls
            .GroupBy(x => new { x.Currency, x.Kind })
            .Select(g => new MoneyTotalDto(
                g.Key.Currency,
                g.Key.Kind,
                g.Sum(x => x.Amount ?? 0),
                g.Count(x => x.Amount != null),
                g.Count(x => x.Amount == null && x.State != "pending")))
            .ToListAsync(ct);
        var models = (await calls
                .GroupBy(x => new { x.Provider, x.ModelId, x.Currency, x.Kind })
                .Select(g => new
                {
                    g.Key.Provider,
                    g.Key.ModelId,
                    g.Key.Currency,
                    g.Key.Kind,
                    Amount = g.Sum(x => x.Amount ?? 0),
                    Requests = g.Count(),
                    Unknown = g.Count(x => x.Amount == null && x.State != "pending"),
                    Input = g.Sum(x => x.InputTokens ?? 0),
                    Output = g.Sum(x => x.OutputTokens ?? 0),
                })
                .ToListAsync(ct))
            .Select(x => new SpendBucketDto(
                Label(x.Provider, x.ModelId, false),
                x.Currency,
                x.Kind,
                x.Amount,
                x.Requests,
                x.Unknown,
                x.Input,
                x.Output))
            .ToArray();
        return new ConversationSpendDto(count + legacy, pending, legacy, totals, MergePresentedModels(models));
    }

    private string Label(string provider, string model, bool administrator) => presentation.DisplayName(model, administrator, provider)!;

    // 隱藏的模型名稱可能對應多個真實模型，它們會被合併到同一個公開標籤。
    private static IReadOnlyList<SpendBucketDto> MergePresentedModels(IEnumerable<SpendBucketDto> rows) => rows
        .GroupBy(x => new { x.Label, x.Currency, x.Kind })
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

    public static byte[] Csv(SpendReportDto report)
    {
        var csv = new StringBuilder("﻿使用者,帳號,幣別,計費類型,已知金額,呼叫次數,未知費用次數\r\n");
        foreach (var user in report.Users)
        {
            csv.AppendLine(string.Join(',', new[]
            {
                CsvCell.Quote(user.DisplayName),
                CsvCell.Quote(user.Account),
                CsvCell.Quote(user.Currency),
                CsvCell.Quote(user.Kind),
                user.Amount.ToString(CultureInfo.InvariantCulture),
                user.Requests.ToString(CultureInfo.InvariantCulture),
                user.UnknownCalls.ToString(CultureInfo.InvariantCulture),
            }));
        }
        return Encoding.UTF8.GetBytes(csv.ToString());
    }
}
