using AiNexus.Data.App.Identity;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using AiNexus.Features.Monitoring.Presence;
using AiNexus.Features.Monitoring.Snapshots;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Monitoring.Traffic;

/// <summary>單一實例的滾動式遙測；基數、取樣數與保留時間都有上限，避免監控本身耗盡記憶體。</summary>
public sealed class RuntimeTraffic : IDisposable
{
    private readonly IOptions<MonitoringOptions> options;
    private readonly TimeProvider clock;
    public const int BucketSeconds = 10;
    /// <summary>健康判定固定讀最後一分鐘的已完成區間，不受檢視者所選時間窗影響。</summary>
    public const int RecentSeconds = 60;
    private const int RetentionSeconds = 900, MaxEndpoints = 128, MaxActivities = 120;
    // 每個保留的十秒區間各一格，外加一格給正在累計的區間。
    private const int ResourceBuckets = RetentionSeconds / BucketSeconds + 1;
    private static readonly double[] LatencyBounds = [1, 2, 5, 10, 20, 30, 50, 75, 100, 150, 200, 300, 500, 750, 1000, 1500, 2000, 3000, 5000, 10000, 30000, 60000, 300000, 600000];
    private readonly object gate = new();
    private readonly SortedDictionary<long, Bucket> buckets = [];
    private readonly Dictionary<string, Session> sessions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dependency> dependencies = new(StringComparer.Ordinal);
    private readonly Queue<TrafficActivity> activities = new();
    private readonly DateTimeOffset started;
    private readonly string instance = Environment.MachineName + " / " + Environment.ProcessId;
    private long sequence, droppedSessions;
    private int inFlight, openStreams;
    private RuntimeResources resources = new(null, 0, 0, null, 0, 0, 0, 0, 0, 0, 0, null, 0);
    private readonly ResourceBucket[] resourceBuckets = Enumerable.Range(0, ResourceBuckets).Select(_ => new ResourceBucket()).ToArray();
    private readonly Meter meter = new("AiNexus.Runtime", "1.0");
    private readonly Counter<long> requestCounter, receivedCounter, sentCounter;
    private readonly Histogram<double> requestDuration;
    public RuntimeTraffic(IOptions<MonitoringOptions> options, TimeProvider clock)
    {
        this.options = options; this.clock = clock; started = clock.GetUtcNow();
        requestCounter = meter.CreateCounter<long>("ainexus.api.requests", "{request}");
        receivedCounter = meter.CreateCounter<long>("ainexus.api.body.received", "By");
        sentCounter = meter.CreateCounter<long>("ainexus.api.body.sent", "By");
        requestDuration = meter.CreateHistogram<double>("ainexus.api.duration", "ms");
        meter.CreateObservableGauge("ainexus.presence.sessions", () => { lock (gate) { Prune(clock.GetUtcNow()); return sessions.Count; } }, "{session}");
        meter.CreateObservableGauge("ainexus.api.in_flight", () => Math.Max(0, Volatile.Read(ref inFlight)), "{request}");
    }
    public bool Enabled => options.Value.Enabled;

    public static string SessionKey(Guid user, Guid session) => user.ToString("N") + ":" + session.ToString("N");
    public void Presence(User user, PresenceRequest request, string address, string browser, string device, string authentication, bool testing)
    {
        if (!Enabled) return;
        lock (gate)
        {
            var now = clock.GetUtcNow(); Prune(now);
            var key = SessionKey(user.Id, request.SessionId);
            if (!sessions.TryGetValue(key, out var session))
            {
                if (sessions.Count >= options.Value.MaxSessions) { droppedSessions++; return; }
                session = new(key, user.Id, now); sessions.Add(key, session);
            }
            session.Account = user.Account; session.Name = user.DisplayName;
            session.Address = address; session.Browser = browser; session.Device = device; session.Authentication = authentication;
            session.Feature = request.Feature; session.State = request.State; session.LastSeen = now; session.Testing = testing;
        }
    }
    public void Leave(Guid user, Guid session) { lock (gate) sessions.Remove(SessionKey(user, session)); }
    public void RequestStarted(bool stream) { if (!Enabled) return; Interlocked.Increment(ref inFlight); if (stream) Interlocked.Increment(ref openStreams); }
    public void StreamStarted() { if (Enabled) Interlocked.Increment(ref openStreams); }
    public void Transferred(long received, long sent)
    {
        if (!Enabled) return;
        if (received > 0) receivedCounter.Add(received);
        if (sent > 0) sentCounter.Add(sent);
        lock (gate) { var now = clock.GetUtcNow(); Current(now).Http.AddBytes(received, sent); }
    }
    public void RequestFinished(Guid? user, Guid? sessionId, string method, string route, int status, double duration, long received, long sent, bool stream, bool cancelled, string? traceId)
    {
        if (!Enabled) return;
        method = method is "GET" or "POST" or "PUT" or "PATCH" or "DELETE" or "HEAD" or "OPTIONS" ? method : "OTHER";
        var feature = MonitoringVocabulary.Feature(route);
        var tags = new TagList { { "feature", feature }, { "http.request.method", method }, { "http.response.status_code", status } };
        requestCounter.Add(1, tags);
        if (!stream && double.IsFinite(duration) && duration >= 0) requestDuration.Record(duration, tags);
        Interlocked.Decrement(ref inFlight); if (stream) Interlocked.Decrement(ref openStreams);
        lock (gate)
        {
            var now = clock.GetUtcNow(); Prune(now);
            var bucket = Current(now); var error = !cancelled && status >= 400;
            bucket.Http.Add(error, error && status >= 500, cancelled, stream ? null : duration, 0, 0);
            var endpointKey = method + " " + route;
            if (!bucket.Endpoints.TryGetValue(endpointKey, out var endpoint) && bucket.Endpoints.Count < MaxEndpoints)
                bucket.Endpoints[endpointKey] = endpoint = new(method, route, feature);
            endpoint?.Stats.Add(error, error && status >= 500, cancelled, stream ? null : duration, received, sent);
            var key = user is { } id && sessionId is { } sid ? SessionKey(id, sid) : null;
            Session? session = null;
            if (key is not null) sessions.TryGetValue(key, out session);
            if (session is not null)
            {
                session.Requests++; if (error) session.Errors++;
                session.Received += received; session.Sent += sent;
                if (method is not ("GET" or "HEAD" or "OPTIONS") || error)
                { session.Action = MonitoringVocabulary.Action(method, feature); session.ActionAt = now; }
            }
            // 輪詢與成功的讀取仍計入 RED 指標，但活動時間軸只保留有意義的操作。
            if (method is not ("GET" or "HEAD" or "OPTIONS") || error || cancelled)
            {
                activities.Enqueue(new(++sequence, now, user, key, session?.Name ?? (user is null ? "未驗證連線" : "已驗證使用者"),
                    feature, MonitoringVocabulary.Action(method, feature), method, route, status, Math.Round(duration, 2), cancelled ? "cancelled" : error ? "error" : "completed", traceId));
                while (activities.Count > MaxActivities) activities.Dequeue();
            }
        }
    }
    public void RegisterDependency(string id, string name, string kind)
    { lock (gate) if (dependencies.Count < 32) dependencies.TryAdd(id, new(name, kind)); }
    public void DependencyStarted(string id) { if (!Enabled) return; lock (gate) if (dependencies.TryGetValue(id, out var dep)) dep.InFlight++; }
    public void DependencyFinished(string id, double duration, bool error, bool cancelled = false)
    {
        if (!Enabled) return;
        lock (gate)
        {
            if (!dependencies.TryGetValue(id, out var dep)) return;
            dep.InFlight = Math.Max(0, dep.InFlight - 1); dep.LastSeen = clock.GetUtcNow();
            Prune(dep.LastSeen.Value);
            var bucket = Current(dep.LastSeen.Value);
            if (!bucket.Dependencies.TryGetValue(id, out var stats)) bucket.Dependencies.Add(id, stats = new());
            stats.Add(error && !cancelled, error && !cancelled, cancelled, duration, 0, 0);
        }
    }
    /// <summary>記錄最新的程序取樣，併入固定環狀陣列中對應的十秒區間。</summary>
    public void SampleResources(RuntimeResources sample)
    {
        lock (gate)
        {
            resources = sample;
            var key = clock.GetUtcNow().ToUnixTimeSeconds() / BucketSeconds * BucketSeconds;
            var bucket = resourceBuckets[(int)(key / BucketSeconds % ResourceBuckets)];
            if (bucket.Key != key) bucket.Reset(key);
            bucket.Add(sample);
        }
    }

    /// <summary>
    /// 1、5 或 15 分鐘時間窗的快照。<paramref name="subsystems"/> 為其他模組的快取讀數；健康判定在釋放鎖之後才進行。
    /// </summary>
    public MonitoringSnapshot Snapshot(int minutes, MonitoredSubsystem[]? subsystems = null)
    {
        // 呼叫端需先以 MonitoringReads.ValidWindow 驗證時間窗。
        if (!MonitoringReads.ValidWindow(minutes)) throw new ArgumentOutOfRangeException(nameof(minutes));
        var snapshot = Collect(minutes, subsystems ?? []);
        return snapshot with { Health = MonitoringHealth.Evaluate(snapshot) };
    }

    private MonitoringSnapshot Collect(int minutes, MonitoredSubsystem[] subsystems)
    {
        lock (gate)
        {
            var now = clock.GetUtcNow(); Prune(now);
            // 只取已完成的十秒區間：數字穩定可比較，圖表點的時間也明確。
            var end = now.ToUnixTimeSeconds() / BucketSeconds * BucketSeconds;
            var since = Math.Max(started.ToUnixTimeSeconds() / BucketSeconds * BucketSeconds, end - minutes * 60);
            var selected = buckets.Where(x => x.Key >= since && x.Key < end).Select(x => x.Value).ToArray();
            var seconds = Math.Max(BucketSeconds, end - since);
            var total = new Stats(); foreach (var bucket in selected) total.Merge(bucket.Http);
            var recentSince = Math.Max(since, end - RecentSeconds);
            var recent = new Stats();
            var timeline = new List<TrafficPoint>(); var resourceTimeline = new List<ResourcePoint>();
            for (var time = since; time < end; time += BucketSeconds)
            {
                var stats = buckets.GetValueOrDefault(time)?.Http ?? new Stats();
                if (time >= recentSince) recent.Merge(stats);
                var at = DateTimeOffset.FromUnixTimeSeconds(time);
                timeline.Add(new(at, stats.Requests / (double)BucketSeconds, Percent(stats.Errors, stats.Requests), stats.ServerErrors, stats.Average, stats.P95(),
                    stats.Received, stats.Sent));
                var sampled = resourceBuckets[(int)(time / BucketSeconds % ResourceBuckets)];
                resourceTimeline.Add(sampled.Key == time ? sampled.Point(at) : new(at, null, null, null, null, null));
            }
            var depTraffic = dependencies.Select(pair => {
                var stats = new Stats(); foreach (var bucket in selected) if (bucket.Dependencies.TryGetValue(pair.Key, out var part)) stats.Merge(part);
                var status = stats.Requests == 0 ? "unobserved" : stats.Errors == stats.Requests ? "failing" : stats.Errors > 0 ? "degraded" : "healthy";
                return new DependencyTraffic(pair.Key, pair.Value.Name, pair.Value.Kind, status, pair.Value.InFlight, pair.Value.LastSeen, stats.Dto(seconds));
            }).ToArray();
            var endpoints = new Dictionary<string, Endpoint>(StringComparer.Ordinal);
            foreach (var bucket in selected) foreach (var pair in bucket.Endpoints)
            {
                if (!endpoints.TryGetValue(pair.Key, out var endpoint)) endpoints[pair.Key] = endpoint = new(pair.Value.Method, pair.Value.Route, pair.Value.Feature);
                endpoint.Stats.Merge(pair.Value.Stats);
            }
            var visibleSessions = sessions.Values.OrderByDescending(x => x.LastSeen).ThenBy(x => x.Id).Take(200).Select(x => x.Dto()).ToArray();
            return new(MonitoringSnapshot.CurrentVersion, Enabled, instance, started, now, minutes, options.Value.RefreshSeconds, options.Value.SessionTimeoutSeconds,
                MonitoringHealth.Pending, sessions.Values.Select(x => x.User).Distinct().Count(), sessions.Count, sessions.Values.Count(x => x.State == "active"),
                Math.Max(0, Volatile.Read(ref inFlight) - Volatile.Read(ref openStreams)), Math.Max(0, Volatile.Read(ref openStreams)),
                Math.Max(0, sessions.Count - visibleSessions.Length), droppedSessions, resources, resourceTimeline.ToArray(), total.Dto(seconds),
                recent.Dto(Math.Max(BucketSeconds, end - recentSince)), timeline.ToArray(), visibleSessions,
                activities.Where(x => x.At >= DateTimeOffset.FromUnixTimeSeconds(since)).Reverse().ToArray(), depTraffic,
                endpoints.Values.OrderByDescending(x => x.Stats.Requests).Take(12).Select(x => new EndpointTraffic(x.Method, x.Route, x.Feature, x.Stats.Dto(seconds))).ToArray(),
                subsystems);
        }
    }
    private Bucket Current(DateTimeOffset now)
    {
        var key = now.ToUnixTimeSeconds() / BucketSeconds * BucketSeconds;
        if (!buckets.TryGetValue(key, out var bucket))
        {
            foreach (var expired in buckets.Keys.TakeWhile(x => x < key - RetentionSeconds).ToArray()) buckets.Remove(expired);
            buckets.Add(key, bucket = new());
        }
        return bucket;
    }
    private void Prune(DateTimeOffset now)
    {
        var expiry = now.AddSeconds(-options.Value.SessionTimeoutSeconds);
        foreach (var key in sessions.Where(x => x.Value.LastSeen < expiry).Select(x => x.Key).ToArray()) sessions.Remove(key);
        var retention = now.ToUnixTimeSeconds() / BucketSeconds * BucketSeconds - RetentionSeconds;
        foreach (var key in buckets.Keys.TakeWhile(x => x < retention).ToArray()) buckets.Remove(key);
        while (activities.TryPeek(out var activity) && activity.At < now.AddSeconds(-RetentionSeconds)) activities.Dequeue();
    }
    private static double Percent(long part, long all) => all == 0 ? 0 : Math.Round(100d * part / all, 2);
    public void Dispose() => meter.Dispose();
    private sealed class Stats
    {
        public long Requests, Errors, ServerErrors, Cancelled, Received, Sent, Timed;
        private double duration;
        private readonly long[] histogram = new long[LatencyBounds.Length + 1];
        public double? Average => Timed == 0 ? null : Math.Round(duration / Timed, 2);
        public void AddBytes(long received, long sent) { Received += Math.Max(0, received); Sent += Math.Max(0, sent); }
        public void Add(bool error, bool serverError, bool cancelled, double? ms, long received, long sent)
        {
            Requests++; if (error) Errors++; if (serverError) ServerErrors++; if (cancelled) Cancelled++;
            Received += Math.Max(0, received); Sent += Math.Max(0, sent);
            if (ms is { } value && double.IsFinite(value) && value >= 0)
            {
                Timed++; duration += value;
                var index = Array.FindIndex(LatencyBounds, bound => value <= bound); histogram[index < 0 ? LatencyBounds.Length : index]++;
            }
        }
        public void Merge(Stats other)
        {
            Requests += other.Requests; Errors += other.Errors; ServerErrors += other.ServerErrors; Cancelled += other.Cancelled;
            Received += other.Received; Sent += other.Sent; Timed += other.Timed; duration += other.duration;
            for (var i = 0; i < histogram.Length; i++) histogram[i] += other.histogram[i];
        }
        /// <summary>第 95 百分位所在直方圖區間的上界；沒有樣本，或落在最後一個上界之外時為 null。</summary>
        public double? P95()
        {
            long sum = 0;
            for (var i = 0; i < histogram.Length && Timed > 0; i++)
            { sum += histogram[i]; if (sum >= Math.Ceiling(Timed * .95)) return i < LatencyBounds.Length ? LatencyBounds[i] : null; }
            return null;
        }
        public TrafficMetrics Dto(double seconds)
            => new(Requests, Errors, ServerErrors, Cancelled, Received, Sent, Math.Round(Requests / seconds, 3), Percent(Errors, Requests), Average, P95());
    }
    private sealed class ResourceBucket
    {
        public long Key { get; private set; } = long.MinValue;
        private double cpu, pause; private int cpuSamples, pauseSamples;
        private long workingSet, heap, queue;
        public void Reset(long key) { Key = key; cpu = pause = 0; cpuSamples = pauseSamples = 0; workingSet = heap = queue = 0; }
        public void Add(RuntimeResources sample)
        {
            if (sample.CpuPercent is { } c) { cpu += c; cpuSamples++; }
            if (sample.GcPausePercent is { } p) { pause += p; pauseSamples++; }
            workingSet = sample.WorkingSetBytes; heap = sample.ManagedHeapBytes; queue = Math.Max(queue, sample.ThreadPoolQueueLength);
        }
        public ResourcePoint Point(DateTimeOffset at) => new(at, cpuSamples == 0 ? null : Math.Round(cpu / cpuSamples, 2), workingSet, heap, queue,
            pauseSamples == 0 ? null : Math.Round(pause / pauseSamples, 2));
    }
    private sealed class Bucket
    { public Stats Http { get; } = new(); public Dictionary<string, Stats> Dependencies { get; } = new(StringComparer.Ordinal); public Dictionary<string, Endpoint> Endpoints { get; } = new(StringComparer.Ordinal); }
    private sealed class Endpoint(string method, string route, string feature)
    { public string Method { get; } = method; public string Route { get; } = route; public string Feature { get; } = feature; public Stats Stats { get; } = new(); }
    private sealed class Dependency(string name, string kind)
    { public string Name { get; } = name; public string Kind { get; } = kind; public int InFlight; public DateTimeOffset? LastSeen; }
    private sealed class Session(string id, Guid user, DateTimeOffset now)
    {
        public string Id { get; } = id; public Guid User { get; } = user; public DateTimeOffset Connected { get; } = now;
        public DateTimeOffset LastSeen = now; public string Account = "", Name = "", Address = "", Browser = "", Device = "", Authentication = "", Feature = "", State = "";
        public bool Testing; public long Requests, Errors, Received, Sent; public string? Action; public DateTimeOffset? ActionAt;
        public OnlineSession Dto() => new(Id, User, Account, Name, Authentication, Address, Browser, Device, Feature, State, Connected, LastSeen, Action, ActionAt, Requests, Errors, Received, Sent, Testing);
    }
}
