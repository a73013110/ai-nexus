using AiNexus.Platform.Errors;

namespace AiNexus.Features.Dashboard;

internal static class DashboardErrors
{
    public static readonly Error InvalidScope = Error.Invalid("invalid_dashboard_scope");
    public static readonly Error AccessDenied = Error.Forbidden("dashboard_access_denied");
}
