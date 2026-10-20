using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.Mail.Delivery;

/// <summary>
/// 每分鐘寄送上限（token bucket：容量＝<c>MaxPerMinute</c>，每分鐘補滿）。程序內即可：單一 IIS 執行個體只有一個 dispatcher（ADR 0001）。
/// 自己實作而不用 <c>TokenBucketRateLimiter</c>，是為了吃 <see cref="TimeProvider"/>，測試才能控制時間。
/// </summary>
public sealed class MailThrottle(IOptions<MailOptions> options, TimeProvider clock)
{
    private readonly Lock gate = new();
    private double tokens = options.Value.MaxPerMinute;
    private DateTimeOffset refilled = clock.GetUtcNow();

    /// <summary>取走最多 <paramref name="wanted"/> 個額度，回傳實際取得的數量（可能為 0）。</summary>
    public int Take(int wanted)
    {
        lock (gate)
        {
            Refill();
            var taken = (int)Math.Min(Math.Floor(tokens), Math.Max(0, wanted));
            tokens -= taken;
            return taken;
        }
    }

    /// <summary>認領到的信比取得的額度少，或這一輪中途停止時歸還。</summary>
    public void Return(int count)
    {
        if (count <= 0) return;
        lock (gate) tokens = Math.Min(options.Value.MaxPerMinute, tokens + count);
    }

    private void Refill()
    {
        var now = clock.GetUtcNow();
        var limit = options.Value.MaxPerMinute;
        var elapsed = (now - refilled).TotalMinutes;
        if (elapsed <= 0) return;
        tokens = Math.Min(limit, tokens + elapsed * limit);
        refilled = now;
    }
}
