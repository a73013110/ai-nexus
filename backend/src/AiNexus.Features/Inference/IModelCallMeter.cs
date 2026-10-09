namespace AiNexus.Features.Inference;

/// <summary>
/// Prices and meters one model call. Billing implements it, so model tasks are charged without this module depending on
/// billing. Each step only tracks changes; the caller saves them.
/// </summary>
public interface IModelCallMeter
{
    /// <summary>Records the call with the price in effect at <paramref name="created"/>.</summary>
    Task ReserveAsync(Guid callId, Guid owner, Guid? conversation, string provider, string model, string operation, DateTimeOffset created, CancellationToken ct);
    Task StartAsync(Guid callId, CancellationToken ct);
    Task MeterAsync(Guid callId, long? input, long? output, long? cached, long? reasoning, CancellationToken ct, bool complete = false);
    Task FinishAsync(Guid callId, string outcome, CancellationToken ct);
}
