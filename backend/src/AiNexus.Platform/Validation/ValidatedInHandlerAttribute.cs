namespace AiNexus.Platform.Validation;

/// <summary>
/// Marks a request body whose rules stay in its handler on purpose, instead of a <see cref="RequestValidator{T}"/>:
/// for example because a 404 or 403 must win over a 400, a failure must be audited inside a transaction, or a format
/// failure must look like any other failure. <c>EndpointConventionTests</c> requires one or the other on every body.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ValidatedInHandlerAttribute(string reason) : Attribute
{
    public string Reason { get; } = reason;
}
