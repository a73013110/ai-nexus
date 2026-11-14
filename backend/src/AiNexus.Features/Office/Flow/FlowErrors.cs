using AiNexus.Platform.Errors;

namespace AiNexus.Features.Office.Flow;

internal static class FlowErrors
{
    public static readonly Error InstanceNotFound = Error.NotFound("flow_instance_not_found");
    public static readonly Error InstanceClosed = Error.Conflict("flow_instance_closed");
    public static readonly Error InstanceChanged = Error.Conflict("flow_instance_changed");
    public static readonly Error NotApprover = Error.Forbidden("flow_not_approver");
    public static readonly Error OwnRequest = Error.Forbidden("flow_own_request");
    public static readonly Error DefinitionMissing = Error.Unavailable("flow_definition_missing");
    public static readonly Error KindNotFound = Error.NotFound("flow_kind_not_found");
    public static readonly Error RuleInvalid = Error.Invalid("flow_rule_invalid");
    public static readonly Error ArgumentInvalid = Error.Invalid("flow_argument_invalid");
    public static readonly Error ConditionInvalid = Error.Invalid("flow_condition_invalid");
    public static readonly Error ViewInvalid = Error.Invalid("flow_view_invalid");
    public static readonly Error CursorInvalid = Error.Invalid("flow_cursor_invalid");
}
