using Microsoft.Extensions.Logging;

namespace AiNexus.IntegrationTests.Support;

/// <summary>只記 EventId 與等級的 logger，驗證「狀態轉換時只記一次」這類日誌行為；以 <c>ActivatorUtilities.CreateInstance</c> 傳給受測類別。</summary>
internal sealed class LogRecorder<T> : ILogger<T>
{
    public List<(int Id, LogLevel Level)> Recorded { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => Recorded.Add((eventId.Id, logLevel));
}
