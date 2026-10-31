using AiNexus.Data.App;
using AiNexus.Data.App.Audit;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AiNexus.Features.Admin.Identity;

/// <summary>
/// 請求稽核（ADR 0023）：宣告 <see cref="AuditedEndpoint"/> 的端點，每個請求都寫一筆稽核，成功記 <c>completed</c>，
/// 失敗記錯誤代碼（handler 回傳的 problem、驗證失敗或例外）。放在解析使用者之後、請求驗證之前，驗證失敗的嘗試也留得下來。
/// </summary>
public static partial class RequestAudit
{
    /// <summary>群組掛了請求稽核的標記：只宣告 <see cref="AuditedEndpoint"/> 卻不在這種群組底下，稽核不會寫入（測試據此檢查）。</summary>
    public sealed record Applied;

    public static RouteGroupBuilder WithAudit(this RouteGroupBuilder group)
    {
        // 與 AddEndpointFilterFactory 相同的掛法，但留住 EndpointBuilder：factory 執行時端點層的稽核宣告已加進 metadata，
        // 而 EndpointFilterFactoryContext 本身拿不到 metadata。
        ((IEndpointConventionBuilder)group.WithMetadata(new Applied())).Add(builder => builder.FilterFactories.Add((context, next) => Create(builder, context, next)));
        return group;
    }

    private static EndpointFilterDelegate Create(EndpointBuilder builder, EndpointFilterFactoryContext context, EndpointFilterDelegate next)
    {
        // 稽核宣告在建置端點時就確定：不稽核的端點不掛過濾器，請求時不必再掃 metadata。
        if (builder.AuditActionOf() is not { } action) return next;
        var logger = context.ApplicationServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(RequestAudit).FullName!);
        return async invocation =>
        {
            var http = invocation.HttpContext;
            // 稽核一定要有身分：功能 policy 與 ICurrentUser 通常已解析過，這裡取的是同一份快取。
            var user = await http.RequestServices.GetRequiredService<CurrentUser>().GetAsync(http.RequestAborted);
            if (!user.IsSuccess) return user.Error.ToProblem();
            var trail = http.RequestServices.GetRequiredService<AuditTrail>();
            trail.Begin(action, user.Value.Id, http);
            object? result;
            try
            {
                result = await next(invocation);
            }
            catch (Exception error)
            {
                // 原本的例外比稽核失敗重要：稽核寫不進去只記日誌，再把原本的例外拋出去。
                try { await trail.CompleteAsync(Code(error, http)); }
                catch (Exception audit) { LogFailureUnrecorded(logger, action.Name, audit.GetType().Name); }
                throw;
            }
            // 成功路徑不吞例外：查詢的稽核寫不進去，資料就不送出。
            await trail.CompleteAsync(Outcome(result));
            return result;
        };
    }

    /// <summary>handler 的回傳值對應的稽核結果：problem 取它的錯誤代碼，其他 4xx／5xx 用預設代碼，其餘為成功。</summary>
    internal static string Outcome(object? result)
    {
        while (result is INestedHttpResult nested) result = nested.Result;
        return result switch
        {
            ProblemHttpResult problem => Problems.CodeOf(problem.ProblemDetails) ?? Problems.DefaultCode(problem.StatusCode),
            IStatusCodeHttpResult { StatusCode: >= 400 } status => Problems.DefaultCode(status.StatusCode.Value),
            _ => AuditEvent.Completed,
        };
    }

    private static string Code(Exception error, HttpContext http) => error switch
    {
        ExternalServiceException external => external.Error.Code,
        OperationCanceledException when http.RequestAborted.IsCancellationRequested => "request_aborted",
        _ => "unexpected_error",
    };

    [LoggerMessage(EventId = 1003, EventName = "audit.failure_unrecorded", Level = LogLevel.Error, Message = "Failed request audit {AuditAction} could not be recorded ({ErrorType}).")]
    private static partial void LogFailureUnrecorded(ILogger logger, string auditAction, string errorType);
}
