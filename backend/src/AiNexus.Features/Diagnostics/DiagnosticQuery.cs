using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Diagnostics;

public sealed class DiagnosticFilter
{
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public LogLevel? Level { get; set; }
    public string? Category { get; set; }
    public int? EventId { get; set; }
    public string? EventName { get; set; }
    public string? IssueCode { get; set; }
    public string? TraceId { get; set; }
    public Guid? JobId { get; set; }
    public Guid? RunId { get; set; }
    public Guid? OperationId { get; set; }
    public string? ErrorCode { get; set; }
    public string? Instance { get; set; }
    public string? Text { get; set; }
    public string? Cursor { get; set; }
    public int? Take { get; set; }
    public string? SortDirection { get; set; }
}
public sealed record DiagnosticSummary(Guid LogId, DateTimeOffset At, string Level, string Category, int EventId, string EventName, string MessageTemplate,
    string? IssueCode, string? TraceId, string? SpanId, string? RequestId, Guid? OperationId, Guid? JobId, Guid? RunId, int? Attempt,
    string? Method, string? Route, int? StatusCode, double? DurationMs, string? ExternalService, string? ErrorCode, string Instance, bool UntrustedClient, string Message);
public sealed record DiagnosticPage(IReadOnlyList<DiagnosticSummary> Events, string? NextCursor, DateTimeOffset From, DateTimeOffset To, DiagnosticHealthDto Health);
public sealed record DiagnosticDetail(DiagnosticSummary Event, string Service, string Environment, string Version, Guid? UserId, string PropertiesJson, string? ExceptionType, string? ExceptionDetail);

/// <summary>Validated, audited reads of the stored diagnostic events for the log slices.</summary>
public sealed class DiagnosticQuery(NexusDbContext db, IDataProtectionProvider protection, IOptions<DiagnosticOptions> options, DiagnosticHealth health, TimeProvider clock)
{
    private readonly IDataProtector cursorProtector = protection.CreateProtector("AiNexus.Diagnostics.Cursor.v1");
    private sealed record CursorData(DateTimeOffset At, Guid LogId, string Fingerprint, Guid UserId);
    public static DiagnosticSummary Describe(DiagnosticEvent x, bool detail = false) => new(x.LogId, x.At, x.Level.ToString(), x.Category, x.EventId, x.EventName, x.MessageTemplate,
        x.IssueCode, x.TraceId, x.SpanId, x.RequestId, x.OperationId, x.JobId, x.RunId, x.Attempt, x.Method, x.Route, x.StatusCode, x.DurationMs, x.ExternalService, x.ErrorCode, x.Instance, x.UntrustedClient, DiagnosticMessage.Render(x, detail));
    private Result<(DateTimeOffset From, DateTimeOffset To)> Validate(DiagnosticFilter filter, bool export = false)
    {
        var to = (filter.To ?? clock.GetUtcNow()).ToUniversalTime(); var from = (filter.From ?? to.AddDays(-1)).ToUniversalTime();
        if (from >= to || to - from > TimeSpan.FromDays(export ? options.Value.MaxExportDays : options.Value.MaxQueryDays)
            || filter.Take is < 1 or > 100 || filter.Level is < LogLevel.Trace or > LogLevel.Critical || filter.EventId < 0
            || filter.Category?.Length > 180 || filter.EventName?.Length > 100 || filter.ErrorCode?.Length > 80 || filter.Instance?.Length > 100
            || filter.SortDirection is not (null or "asc" or "desc")
            || filter.Text?.Length > 72 || filter.Cursor?.Length > 2048 || filter.IssueCode is not null && !Issues.ValidCode(filter.IssueCode)
            || filter.TraceId is not null && (filter.TraceId.Length != 32 || !filter.TraceId.All(char.IsAsciiHexDigit)))
            return DiagnosticsErrors.FilterInvalid;
        // Text scans have an explicit narrow time bound; exact correlation lookups use ordinary indexes.
        if (!string.IsNullOrWhiteSpace(filter.Text) && to - from > TimeSpan.FromDays(1)) return DiagnosticsErrors.TextRange;
        return (from, to);
    }
    private IQueryable<DiagnosticEvent> Filter(DiagnosticFilter f, DateTimeOffset from, DateTimeOffset to)
    {
        var query = db.Set<DiagnosticEvent>().AsNoTracking().Where(x => x.At >= from && x.At <= to);
        if (f.Level is { } level) query = query.Where(x => x.Level == level);
        if (f.Category is { Length: > 0 } category) query = query.Where(x => x.Category == category);
        if (f.EventId is { } eventId) query = query.Where(x => x.EventId == eventId);
        if (f.EventName is { Length: > 0 } eventName) query = query.Where(x => x.EventName == eventName);
        if (f.IssueCode is { } issue) query = query.Where(x => x.IssueCode == issue);
        if (f.TraceId is { } trace) query = query.Where(x => x.TraceId == trace);
        if (f.JobId is { } job) query = query.Where(x => x.JobId == job);
        if (f.RunId is { } run) query = query.Where(x => x.RunId == run);
        if (f.OperationId is { } operation) query = query.Where(x => x.OperationId == operation);
        if (f.ErrorCode is { Length: > 0 } error) query = query.Where(x => x.ErrorCode == error);
        if (f.Instance is { Length: > 0 } instance) query = query.Where(x => x.Instance == instance);
        if (f.Text is { Length: > 0 } text) query = query.Where(x => x.MessageTemplate.Contains(text));
        return query;
    }
    public async Task<Result<DiagnosticPage>> ListAsync(Guid actor, DiagnosticFilter filter, CancellationToken ct)
    {
        using var suppress = DiagnosticSuppression.Enter();
        var range = Validate(filter);
        if (!range.IsSuccess) return range.Error;
        var (from, to) = range.Value;
        await AuditAsync(actor, "logs.query", null, filter, ct);
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { from, to, filter.Level, filter.Category, filter.EventId, filter.EventName, filter.IssueCode, filter.TraceId, filter.JobId, filter.RunId, filter.OperationId, filter.ErrorCode, filter.Instance, filter.Text, sortDirection = filter.SortDirection ?? "desc", take = filter.Take ?? 50 }))));
        var query = Filter(filter, from, to); db.Database.SetCommandTimeout(options.Value.SqlTimeoutSeconds);
        if (filter.Cursor is { Length: > 0 } token)
        {
            CursorData cursor;
            try { cursor = JsonSerializer.Deserialize<CursorData>(cursorProtector.Unprotect(token)) ?? throw new JsonException(); }
            catch (Exception e) when (e is CryptographicException or JsonException or FormatException) { return DiagnosticsErrors.CursorInvalid; }
            if (cursor.UserId != actor || cursor.Fingerprint != fingerprint) return DiagnosticsErrors.CursorInvalid;
            query = filter.SortDirection == "asc"
                ? query.Where(x => x.At > cursor.At || x.At == cursor.At && x.LogId.CompareTo(cursor.LogId) > 0)
                : query.Where(x => x.At < cursor.At || x.At == cursor.At && x.LogId.CompareTo(cursor.LogId) < 0);
        }
        var take = filter.Take ?? 50;
        // Projection keeps privileged stacks/properties out of list responses and prevents large materialization.
        var ordered = Order(query, filter.SortDirection);
        var rows = await ordered.Select(x => new DiagnosticEvent {
            LogId = x.LogId, At = x.At, Level = x.Level, Category = x.Category, EventId = x.EventId, EventName = x.EventName, MessageTemplate = x.MessageTemplate,
            IssueCode = x.IssueCode, TraceId = x.TraceId, SpanId = x.SpanId, RequestId = x.RequestId, OperationId = x.OperationId, JobId = x.JobId, RunId = x.RunId, Attempt = x.Attempt,
            Method = x.Method, Route = x.Route, StatusCode = x.StatusCode, DurationMs = x.DurationMs, ExternalService = x.ExternalService, ErrorCode = x.ErrorCode, Instance = x.Instance, UntrustedClient = x.UntrustedClient
        }).Take(take + 1).ToArrayAsync(ct);
        var last = rows.Take(take).LastOrDefault();
        return new DiagnosticPage(rows.Take(take).Select(x => Describe(x)).ToArray(), rows.Length > take && last is not null ? cursorProtector.Protect(JsonSerializer.Serialize(new CursorData(last.At, last.LogId, fingerprint, actor))) : null, from, to, health.Snapshot());
    }
    private static IOrderedQueryable<DiagnosticEvent> Order(IQueryable<DiagnosticEvent> query, string? direction) =>
        direction == "asc" ? query.OrderBy(x => x.At).ThenBy(x => x.LogId) : query.OrderByDescending(x => x.At).ThenByDescending(x => x.LogId);
    public async Task<Result<DiagnosticDetail>> DetailAsync(Guid actor, Guid id, CancellationToken ct)
    {
        using var suppress = DiagnosticSuppression.Enter(); await AuditAsync(actor, "logs.detail", id, null, ct);
        db.Database.SetCommandTimeout(options.Value.SqlTimeoutSeconds);
        var item = await db.Set<DiagnosticEvent>().AsNoTracking().SingleOrDefaultAsync(x => x.LogId == id, ct);
        if (item is null) return DiagnosticsErrors.NotFound;
        return new DiagnosticDetail(Describe(item, detail: true), item.Service, item.Environment, item.Version, item.UserId, item.PropertiesJson, item.ExceptionType, item.ExceptionDetail);
    }
    public async Task<DiagnosticHealthDto> HealthAsync(Guid actor, CancellationToken ct)
    {
        using var suppress = DiagnosticSuppression.Enter();
        await AuditAsync(actor, "logs.health", null, null, ct); return health.Snapshot();
    }
    public async Task<Result<string>> ExportAsync(Guid actor, DiagnosticFilter filter, CancellationToken ct)
    {
        using var suppress = DiagnosticSuppression.Enter();
        var range = Validate(filter, export: true);
        if (!range.IsSuccess) return range.Error;
        var (from, to) = range.Value;
        if (filter.Cursor is { Length: > 0 }) return DiagnosticsErrors.ExportCursor;
        await AuditAsync(actor, "logs.export", null, filter, ct);
        db.Database.SetCommandTimeout(options.Value.SqlTimeoutSeconds);
        var rows = await Order(Filter(filter, from, to), filter.SortDirection)
            .Select(x => new { x.LogId, x.At, x.Level, x.Category, x.EventName, x.IssueCode, x.TraceId, x.JobId, x.RunId, x.ErrorCode, x.Instance })
            .Take(options.Value.MaxExportRows + 1).ToArrayAsync(ct);
        if (rows.Length > options.Value.MaxExportRows) return DiagnosticsErrors.ExportLimit;
        var csv = new StringBuilder("\uFEFFLogId,UTC,Level,Category,Event,IssueCode,TraceId,JobId,RunId,ErrorCode,Instance\r\n");
        foreach (var row in rows) csv.AppendLine(string.Join(",", new object?[] { row.LogId, row.At.ToUniversalTime().ToString("O"), row.Level, row.Category, row.EventName, row.IssueCode, row.TraceId, row.JobId, row.RunId, row.ErrorCode, row.Instance }.Select(Csv)));
        return csv.ToString();
    }
    public static string Csv(object? value)
    {
        var text = DiagnosticRedactor.Text(Convert.ToString(value, CultureInfo.InvariantCulture), 240);
        if (text.TrimStart().StartsWith('=') || text.TrimStart().StartsWith('+') || text.TrimStart().StartsWith('-') || text.TrimStart().StartsWith('@')) text = "'" + text;
        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }
    private async Task AuditAsync(Guid actor, string action, Guid? logId, DiagnosticFilter? filter, CancellationToken ct)
    {
        // Privileged reads fail closed if the audit cannot be persisted. This is separate from diagnostic sampling/queues.
        db.AuditEvents.Add(new() { OwnerId = actor, Action = action, ResourceId = logId, Result = "read", DetailsJson = JsonSerializer.Serialize(new { from = filter?.From, to = filter?.To, issueCode = filter?.IssueCode, traceId = filter?.TraceId, jobId = filter?.JobId, runId = filter?.RunId }) });
        await db.SaveChangesAsync(ct);
    }
}
