using AiNexus.Features.Identity;
using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Http;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.Extensions.Caching.Memory;

namespace AiNexus.Features.Diagnostics;

public sealed record ClientIssueRequest(string Kind, string Fingerprint);
public sealed record ClientIssueResponse(string IssueCode, bool Accepted);

internal sealed class ClientIssueRequestValidator : RequestValidator<ClientIssueRequest>
{
    public override string ProblemCode => "client_issue_invalid";

    public ClientIssueRequestValidator()
    {
        RuleFor(x => x.Kind).Must(x => x is "exception" or "rejection").WithErrorCode("unknown");
        RuleFor(x => x.Fingerprint).Must(x => x is { Length: 64 } && x.All(char.IsAsciiHexDigit)).WithErrorCode("format");
    }
}

/// <summary>
/// An untrusted browser report of an unhandled exception or rejection, logged once per user and fingerprint a minute.
/// Only the kind and a hash reach the log; the issue code is accepted when the diagnostic queue admits the event.
/// </summary>
internal static partial class ReportClientIssue
{
    public static void Map(RouteGroupBuilder api) => api
        .MapPost("/client-issues", (ClientIssueRequest request, ICurrentUser user, ClientIssueDeduplication dedup, ILogger<ClientIssueDeduplication> logger) =>
            Results.Ok(dedup.Record(user.Id, request.Kind + request.Fingerprint, code => Log(logger, user.Id, request, code))))
        .RequireRateLimiting(DiagnosticsModule.ClientIssueRateLimit).WithRequestBodyLimit(2048).WithName("ReportClientIssue").Produces<ClientIssueResponse>();

    private static bool Log(ILogger logger, Guid user, ClientIssueRequest request, string code)
    {
        var receipt = new DiagnosticDelivery();
        using var scope = logger.BeginScope(new Dictionary<string, object?> { ["NexusDelivery"] = receipt, ["IssueCode"] = code, ["UserId"] = user, ["UntrustedClient"] = true, ["ClientKind"] = request.Kind, ["ClientFingerprint"] = request.Fingerprint });
        LogClientIssue(logger, request.Kind, request.Fingerprint);
        // Queue admission is not a synchronous durable-storage acknowledgement.
        return receipt.Accepted;
    }

    [LoggerMessage(EventId = 4001, EventName = "client.unhandled", Level = LogLevel.Warning, Message = "Untrusted client reported {ClientKind}; fingerprint {ClientFingerprint}.")]
    private static partial void LogClientIssue(ILogger logger, string clientKind, string clientFingerprint);
}

public sealed class ClientIssueDeduplication : IDisposable
{
    private readonly MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = 10000 });
    private readonly object gate = new();
    public ClientIssueResponse Record(Guid user, string fingerprint, Func<string, bool> admit)
    {
        lock (gate)
        {
            var key = user.ToString("N") + fingerprint;
            if (cache.TryGetValue<string>(key, out var existing)) return new(existing!, true);
            var code = Issues.NewCode();
            if (!admit(code)) return new(code, false);
            cache.Set(key, code, new MemoryCacheEntryOptions { Size = 1, AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1) });
            return new(code, true);
        }
    }
    public void Dispose() => cache.Dispose();
}
