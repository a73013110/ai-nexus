using AiNexus.Platform.Errors;

namespace AiNexus.Features.Admin.Billing;

internal static class BillingErrors
{
    public static readonly Error PriceExists = Error.Conflict("price_exists");
    public static readonly Error InvalidSpendPeriod = Error.Invalid("invalid_spend_period");
    public static readonly Error ConversationNotFound = Error.NotFound("conversation_not_found");
    public static readonly Error InvalidPrice = Error.Invalid("invalid_price");
}
