using System.Diagnostics;

namespace AiNexus.Features.Monitoring;

/// <summary>Shared by every factory client; a call ends at response headers, independently of model streaming.</summary>
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
