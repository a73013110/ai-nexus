using AiNexus.Platform.Diagnostics;
using Microsoft.Data.SqlClient;

namespace AiNexus.Features.Knowledge;

public static class RetrievalDiagnostics
{
    public static string Degraded(ILogger logger, string requested, string actual, string reason, Exception? exception = null, string service = "sqlserver")
    {
        var code = Issues.NewCode();
        var number = (exception as SqlException)?.Number;
        using var scope = logger.BeginScope(new Dictionary<string, object?> { ["IssueCode"] = code, ["RequestedMode"] = requested, ["ActualMode"] = actual, ["ErrorCode"] = reason, ["SqlNumber"] = number, ["ExternalService"] = service });
        logger.LogWarning(DiagnosticEvents.Degraded, exception, "Retrieval degraded from {RequestedMode} to {ActualMode}; reason {ErrorCode}, SQL {SqlNumber}, issue {IssueCode}.", requested, actual, reason, number, code);
        return code;
    }
}
