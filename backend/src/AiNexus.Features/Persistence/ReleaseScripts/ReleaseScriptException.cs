namespace AiNexus.Features.Persistence.ReleaseScripts;

/// <summary>The release SQL cannot be produced for the requested range; the message tells the operator why.</summary>
public sealed class ReleaseScriptException : Exception
{
    public ReleaseScriptException() { }

    public ReleaseScriptException(string message) : base(message) { }

    public ReleaseScriptException(string message, Exception innerException) : base(message, innerException) { }
}
