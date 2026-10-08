namespace AiNexus.Platform.Errors;

public sealed class ApiException(int status, string code, string message, Exception? inner = null) : Exception(message, inner)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}
