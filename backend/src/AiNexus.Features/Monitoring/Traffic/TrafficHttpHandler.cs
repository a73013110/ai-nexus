using System.Diagnostics;

namespace AiNexus.Features.Monitoring.Traffic;

/// <summary>所有工廠建立的 HTTP 用戶端共用；呼叫在回應標頭送達時即結束計時，與模型串流的長短無關。</summary>
public sealed class TrafficHttpHandler(RuntimeTraffic traffic, DependencyCatalog catalog) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (!traffic.Enabled || MonitoringSuppression.Active) return await base.SendAsync(request, ct);
        var id = catalog.Http(request.RequestUri); var start = Stopwatch.GetTimestamp(); traffic.DependencyStarted(id);
        var error = true; var cancelled = false;
        try { var response = await base.SendAsync(request, ct); error = !response.IsSuccessStatusCode; return response; }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { cancelled = true; throw; }
        finally { traffic.DependencyFinished(id, Stopwatch.GetElapsedTime(start).TotalMilliseconds, error, cancelled); }
    }
}
