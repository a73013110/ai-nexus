using System.Globalization;
using System.Text;
using AiNexus.Platform.Errors;
using AiNexus.Features.Persistence;
using AiNexus.Features.Inference;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Billing;

/// <summary>A validated reporting window: 1 to 366 days, with the viewer's UTC offset for day buckets.</summary>
public sealed record SpendPeriod(DateTimeOffset From, DateTimeOffset Until, int Offset)
{
    public static Result<SpendPeriod> Create(DateTimeOffset? from, DateTimeOffset? until, int? offset, DateTimeOffset now)
    {
        var end = until ?? now; var start = from ?? end.AddDays(-30); var zone = offset ?? 480;
        if (start >= end || end - start > TimeSpan.FromDays(366) || zone is < -840 or > 840) return Error.Invalid("invalid_spend_period");
        return new SpendPeriod(start.ToUniversalTime(), end.ToUniversalTime(), zone);
    }
}

public sealed class SpendReports(NexusDbContext db, ModelPresentation presentation)
{
    /// <summary>For callers outside this module that still report invalid periods by exception.</summary>
    public static SpendPeriod Period(DateTimeOffset? from, DateTimeOffset? until, int? offset)
    {
        var period = SpendPeriod.Create(from, until, offset, DateTimeOffset.UtcNow);
        return period.IsSuccess ? period.Value : throw new ApiException(400, period.Error.Code, "");
    }
    public Task<SpendReportDto> ReportAsync(Guid? owner, DateTimeOffset? from, DateTimeOffset? until, int? offset, bool administrator, CancellationToken ct)
        => ReportAsync(owner, Period(from, until, offset), administrator, ct);
    public async Task<SpendReportDto> ReportAsync(Guid? owner, SpendPeriod p, bool administrator, CancellationToken ct)
    {
        var query = db.Set<ModelCharge>().AsNoTracking().Where(x => x.CreatedAt >= p.From && x.CreatedAt < p.Until);
        if (owner is Guid id) query = query.Where(x => x.OwnerId == id);
        // Aggregates stay in SQL. SQLite is used only by the isolated test host.
        var groups = query.GroupBy(x => new { x.Currency, x.Kind }).Select(g => new MoneyTotalDto(g.Key.Currency, g.Key.Kind,
            g.Sum(x => x.Amount ?? 0), g.Count(x => x.Amount != null), g.Count(x => x.Amount == null && x.State != "pending")));
        IReadOnlyList<MoneyTotalDto> totals;
        IReadOnlyList<SpendBucketDto> daily, models;
        IReadOnlyList<SpendUserDto> users = [];
        var requests = await query.CountAsync(ct); var pending = await query.CountAsync(x => x.State == "pending", ct);
        long input = 0, output = 0;
        if (!db.Database.IsSqlServer())
        {
            var rows = await query.ToListAsync(ct);
            totals = Totals(rows);
            daily = rows.GroupBy(x => new { Day = x.CreatedAt.ToOffset(TimeSpan.FromMinutes(p.Offset)).ToString("yyyy-MM-dd"), x.Currency, x.Kind })
                .OrderBy(g => g.Key.Day).Select(g => Bucket(g.Key.Day, g)).ToArray();
            models = ModelBuckets(rows, administrator);
            input = rows.Sum(x => x.InputTokens ?? 0); output = rows.Sum(x => x.OutputTokens ?? 0);
            if (administrator)
            {
                var identities = await db.Users.AsNoTracking().ToDictionaryAsync(x => x.Id, ct);
                users = rows.GroupBy(x => new { x.OwnerId, x.Currency, x.Kind }).Select(g => new SpendUserDto(g.Key.OwnerId,
                    identities[g.Key.OwnerId].DisplayName, identities[g.Key.OwnerId].Account, g.Key.Currency, g.Key.Kind,
                    g.Sum(x => x.Amount ?? 0), g.Count(), g.Count(x => x.Amount == null && x.State != "pending"))).ToArray();
            }
        }
        else
        {
            totals = await groups.ToListAsync(ct);
            daily = (await query.GroupBy(x => new { Day = x.CreatedAt.AddMinutes(p.Offset).Date, x.Currency, x.Kind })
                .Select(g => new { g.Key.Day, g.Key.Currency, g.Key.Kind, Amount = g.Sum(x => x.Amount ?? 0), Requests = g.Count(),
                    Unknown = g.Count(x => x.Amount == null && x.State != "pending"), Input = g.Sum(x => x.InputTokens ?? 0), Output = g.Sum(x => x.OutputTokens ?? 0) })
                .OrderBy(x => x.Day).ToListAsync(ct)).Select(x => new SpendBucketDto(x.Day.ToString("yyyy-MM-dd"), x.Currency, x.Kind, x.Amount, x.Requests, x.Unknown, x.Input, x.Output)).ToArray();
            models = (await query.GroupBy(x => new { x.Provider, x.ModelId, x.Currency, x.Kind }).Select(g => new { g.Key.Provider, g.Key.ModelId,
                g.Key.Currency, g.Key.Kind, Amount = g.Sum(x => x.Amount ?? 0), Requests = g.Count(), Unknown = g.Count(x => x.Amount == null && x.State != "pending"),
                Input = g.Sum(x => x.InputTokens ?? 0), Output = g.Sum(x => x.OutputTokens ?? 0) }).OrderByDescending(x => x.Requests).ToListAsync(ct))
                .Select(x => new SpendBucketDto(Label(x.Provider, x.ModelId, administrator), x.Currency, x.Kind, x.Amount, x.Requests, x.Unknown, x.Input, x.Output)).ToArray();
            input = await query.SumAsync(x => x.InputTokens ?? 0, ct); output = await query.SumAsync(x => x.OutputTokens ?? 0, ct);
            if (administrator)
                users = await (from g in query.GroupBy(x => new { x.OwnerId, x.Currency, x.Kind })
                    select new { g.Key.OwnerId, g.Key.Currency, g.Key.Kind, Amount = g.Sum(x => x.Amount ?? 0), Requests = g.Count(), Unknown = g.Count(x => x.Amount == null && x.State != "pending") })
                    .Join(db.Users, x => x.OwnerId, u => u.Id, (x, u) => new { u.Id, u.DisplayName, u.Account, x.Currency, x.Kind, x.Amount, x.Requests, x.Unknown })
                    .OrderByDescending(x => x.Requests).Select(x => new SpendUserDto(x.Id, x.DisplayName, x.Account, x.Currency, x.Kind, x.Amount, x.Requests, x.Unknown)).ToListAsync(ct);
        }
        var legacy = await db.Runs.CountAsync(x => (owner == null || x.OwnerId == owner) && x.CreatedAt >= p.From && x.CreatedAt < p.Until && !db.Set<ModelCharge>().Any(c => c.Id == x.Id), ct)
            + await db.Set<ModelInvocation>().CountAsync(x => (owner == null || x.OwnerId == owner) && x.CreatedAt >= p.From && x.CreatedAt < p.Until && !db.Set<ModelCharge>().Any(c => c.Id == x.Id), ct);
        return new(p.From, p.Until, p.Offset, requests + legacy, pending, legacy, input, output, totals, daily, MergePresentedModels(models), users);
    }
    public async Task<Result<ConversationSpendDto>> ConversationAsync(Guid owner, Guid id, CancellationToken ct)
    {
        if (!await db.Conversations.AnyAsync(x => x.Id == id && x.OwnerId == owner && !x.IsDeleted, ct)) return Error.NotFound("conversation_not_found");
        var calls = db.Set<ModelCharge>().AsNoTracking().Where(x => x.OwnerId == owner && x.ConversationId == id);
        var legacy = await db.Runs.CountAsync(x => x.ConversationId == id && !db.Set<ModelCharge>().Any(c => c.Id == x.Id), ct);
        var count = await calls.CountAsync(ct); var pending = await calls.CountAsync(x => x.State == "pending", ct);
        IReadOnlyList<MoneyTotalDto> totals; IReadOnlyList<SpendBucketDto> models;
        if (!db.Database.IsSqlServer())
        {
            var rows = await calls.ToListAsync(ct); totals = Totals(rows); models = ModelBuckets(rows, false);
        }
        else
        {
            totals = await calls.GroupBy(x => new { x.Currency, x.Kind }).Select(g => new MoneyTotalDto(g.Key.Currency, g.Key.Kind,
                g.Sum(x => x.Amount ?? 0), g.Count(x => x.Amount != null), g.Count(x => x.Amount == null && x.State != "pending"))).ToListAsync(ct);
            models = (await calls.GroupBy(x => new { x.Provider, x.ModelId, x.Currency, x.Kind }).Select(g => new { g.Key.Provider, g.Key.ModelId,
                g.Key.Currency, g.Key.Kind, Amount = g.Sum(x => x.Amount ?? 0), Requests = g.Count(), Unknown = g.Count(x => x.Amount == null && x.State != "pending"),
                Input = g.Sum(x => x.InputTokens ?? 0), Output = g.Sum(x => x.OutputTokens ?? 0) }).ToListAsync(ct))
                .Select(x => new SpendBucketDto(Label(x.Provider, x.ModelId, false), x.Currency, x.Kind, x.Amount, x.Requests, x.Unknown, x.Input, x.Output)).ToArray();
        }
        return new ConversationSpendDto(count + legacy, pending, legacy, totals, MergePresentedModels(models));
    }
    private string Label(string provider, string model, bool administrator) => presentation.DisplayName(model, administrator, provider)!;
    private static SpendBucketDto Bucket(string label, IEnumerable<ModelCharge> calls) => new(label, calls.First().Currency, calls.First().Kind,
        calls.Sum(x => x.Amount ?? 0), calls.Count(), calls.Count(x => x.Amount == null && x.State != "pending"), calls.Sum(x => x.InputTokens ?? 0), calls.Sum(x => x.OutputTokens ?? 0));
    private IReadOnlyList<SpendBucketDto> ModelBuckets(IEnumerable<ModelCharge> calls, bool administrator) => calls
        .GroupBy(x => new { x.Provider, x.ModelId, x.Currency, x.Kind }).Select(g => Bucket(Label(g.Key.Provider, g.Key.ModelId, administrator), g)).ToArray();
    // A hidden model name may map several real models to the same public label.
    private static IReadOnlyList<SpendBucketDto> MergePresentedModels(IEnumerable<SpendBucketDto> rows) => rows
        .GroupBy(x => new { x.Label, x.Currency, x.Kind }).Select(g => new SpendBucketDto(g.Key.Label, g.Key.Currency, g.Key.Kind,
            g.Sum(x => x.Amount), g.Sum(x => x.Requests), g.Sum(x => x.UnknownCalls), g.Sum(x => x.InputTokens), g.Sum(x => x.OutputTokens)))
        .OrderByDescending(x => x.Requests).ToArray();
    private static IReadOnlyList<MoneyTotalDto> Totals(IEnumerable<ModelCharge> calls) => calls.GroupBy(x => new { x.Currency, x.Kind })
        .Select(g => new MoneyTotalDto(g.Key.Currency, g.Key.Kind, g.Sum(x => x.Amount ?? 0), g.Count(x => x.Amount != null), g.Count(x => x.Amount == null && x.State != "pending"))).ToArray();
    public static byte[] Csv(SpendReportDto report)
    {
        var csv = new StringBuilder("\uFEFF使用者,帳號,幣別,計費類型,已知金額,呼叫次數,未知費用次數\r\n");
        foreach (var user in report.Users)
            csv.AppendLine(string.Join(',', new[] { Cell(user.DisplayName), Cell(user.Account), Cell(user.Currency), Cell(user.Kind),
                user.Amount.ToString(CultureInfo.InvariantCulture), user.Requests.ToString(), user.UnknownCalls.ToString() }));
        return Encoding.UTF8.GetBytes(csv.ToString());
    }
    private static string Cell(string text) => "\"" + (text.TrimStart().StartsWithAny('=', '+', '-', '@', '\t', '\r') ? "'" : "") + text.Replace("\"", "\"\"") + "\"";
}

internal static class CsvTextExtensions
{
    public static bool StartsWithAny(this string text, params char[] characters) => text.Length > 0 && characters.Contains(text[0]);
}
