namespace AiNexus.Features.Work.Chat.Runs;

/// <summary>
/// SSE 事件串流訂閱門閥控制。
/// 限制單一使用者最多同時開啟 2 個事件串流，全系統最多 64 個同時連線，
/// 防止惡意或異常客戶端開大量連線耗盡 Web 伺服器線程與記憶體。
/// </summary>
public sealed class SubscriptionLimits
{
    private readonly object gate = new();
    private readonly Dictionary<Guid, int> users = [];
    private int total;

    /// <summary>
    /// 嘗試取得一個訂閱名額租約。若超過上限則回傳 null。
    /// 當串流連線結束時 Dispose 租約將自動釋放名額。
    /// </summary>
    public IDisposable? TryAcquire(Guid owner)
    {
        lock (gate)
        {
            var count = users.GetValueOrDefault(owner);
            if (count >= 2 || total >= 64) return null;
            users[owner] = count + 1;
            total++;
        }

        return new Lease(() =>
        {
            lock (gate)
            {
                if (--users[owner] == 0) users.Remove(owner);
                total--;
            }
        });
    }

    private sealed class Lease(Action release) : IDisposable
    {
        private Action? action = release;
        public void Dispose() => Interlocked.Exchange(ref action, null)?.Invoke();
    }
}
