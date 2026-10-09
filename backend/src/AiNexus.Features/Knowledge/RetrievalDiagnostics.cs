using AiNexus.Platform.Diagnostics;
using Microsoft.Data.SqlClient;

namespace AiNexus.Features.Knowledge;

public static partial class RetrievalDiagnostics
{
    public static string Degraded(ILogger logger, string requested, string actual, string reason, Exception? exception = null, string service = "sqlserver")
    {
        var code = Issues.NewCode();
        var number = (exception as SqlException)?.Number;
        using var scope = logger.BeginScope(new Dictionary<string, object?> { ["IssueCode"] = code, ["RequestedMode"] = requested, ["ActualMode"] = actual, ["ErrorCode"] = reason, ["SqlNumber"] = number, ["ExternalService"] = service });
        LogDegraded(logger, exception, requested, actual, reason, number, code);
        return code;
    }

    [LoggerMessage(EventId = DiagnosticEvents.Degraded, EventName = "retrieval.degraded", Level = LogLevel.Warning, Message = "Retrieval degraded from {RequestedMode} to {ActualMode}; reason {ErrorCode}, SQL {SqlNumber}, issue {IssueCode}.")]
    private static partial void LogDegraded(ILogger logger, Exception? exception, string requestedMode, string actualMode, string errorCode, int? sqlNumber, string issueCode);
}
