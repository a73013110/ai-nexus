namespace AiNexus.Features.Persistence.ReleaseScripts;

/// <summary>無法為指定範圍產生發布 SQL；訊息會說明原因。</summary>
public sealed class ReleaseScriptException : Exception
{
    public ReleaseScriptException() { }

    public ReleaseScriptException(string message) : base(message) { }

    public ReleaseScriptException(string message, Exception innerException) : base(message, innerException) { }
}
