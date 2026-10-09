namespace AiNexus.Platform.Errors;

/// <summary>
/// A system outside this process (model provider, directory, Git host, external database) failed or refused, at a point
/// where no <see cref="Result"/> can be returned: inside a provider stream, an HTTP client or a capability probe.
/// Expected failures of a use case are <see cref="Error"/> values, never this exception. <see cref="Exception.Message"/>
/// is operator detail for logs and host commands; the public response only carries <see cref="Error"/>.
/// </summary>
public sealed class ExternalServiceException(Error error, string? detail = null, Exception? inner = null)
    : Exception(detail ?? error.Code, inner)
{
    public Error Error { get; } = error;
}
