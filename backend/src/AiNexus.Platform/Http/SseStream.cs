using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;

namespace AiNexus.Platform.Http;

/// <summary>客戶端太久收不下資料，連線已切斷。等同客戶端離開：不是伺服器錯誤，也不是上游逾時。</summary>
public sealed class SseClientStalledException : OperationCanceledException;

/// <summary>
/// 一條 SSE 回應：第一次寫入才送表頭，之前都還能回一般的錯誤回應。
/// 每次寫入限時，慢客戶端不會一直佔住連線與串流上限；逾時就切斷連線並拋出 <see cref="SseClientStalledException"/>。
/// </summary>
public sealed class SseStream(HttpContext http, TimeProvider clock)
{
    private static readonly TimeSpan WriteLimit = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(10);

    private bool started;
    private DateTimeOffset lastWrite;

    public Task SendAsync<T>(string name, T value, JsonSerializerOptions options, CancellationToken ct, long? id = null)
        => WriteAsync($"{(id is { } sequence ? $"id: {sequence}\n" : "")}event: {name}\ndata: {JsonSerializer.Serialize(value, options)}\n\n", ct);

    public Task CommentAsync(string text, CancellationToken ct) => WriteAsync($": {text}\n\n", ct);

    /// <summary>閒置超過心跳間隔才送註解行，防止中介 Proxy 切斷安靜的連線；有事件在送時不必多送。</summary>
    public Task HeartbeatIfIdleAsync(CancellationToken ct)
        => clock.GetUtcNow() - lastWrite > HeartbeatInterval ? CommentAsync("heartbeat", ct) : Task.CompletedTask;

    private async Task WriteAsync(string frame, CancellationToken ct)
    {
        if (!started) Start();
        using var limit = new CancellationTokenSource(WriteLimit, clock);
        using var write = CancellationTokenSource.CreateLinkedTokenSource(ct, limit.Token);
        try
        {
            await http.Response.WriteAsync(frame, write.Token);
            await http.Response.Body.FlushAsync(write.Token);
        }
        catch (OperationCanceledException) when (limit.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            // 普通的取消例外在 ct 沒被取消時，上層分不出是誰逾時（模型呼叫會當成模型逾時）；連線也要切斷，否則收尾的錯誤事件又卡在同一個客戶端。
            http.Abort();
            throw new SseClientStalledException();
        }
        lastWrite = clock.GetUtcNow();
    }

    private void Start()
    {
        started = true;
        http.Response.ContentType = "text/event-stream; charset=utf-8";
        // 關閉代理（如 nginx）與 ASP.NET 的回應緩衝，否則事件會被攢住，畫面看不到即時內容。
        http.Response.Headers["X-Accel-Buffering"] = "no";
        http.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
    }
}
