using System.Data.Common;
using System.Text.Json;
using AiNexus.Features.AccessControl;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Integrations;

/// <summary>
/// The only path to the read-only legacy sources. Authorization is checked before every query and again after it, so a
/// grant revoked mid-query never releases data; every read is audited.
/// </summary>
internal sealed class SourceGateway(NexusDbContext db, AccessService access, IOptions<IntegrationsOptions> options, IConfiguration config,
    IEnumerable<IControlledSourceAdapter> adapters, ILogger<SourceGateway> logger)
{
    private static readonly (string Id, string Name, string Description, string[] Kinds)[] Catalog = [
        ("gdweb", "公文系統", "查詢授權文件、狀態、版本與簽核歷程。", ["document"]),
        ("meiho", "校務系統", "先提供核准的規範與單位資料；學生資料須另建逐筆授權 adapter。", ["reference", "organization"])
    ];

    private sealed record Target(IControlledSourceAdapter Adapter, SourceOptions Options, SourceActor Actor);

    public static bool ValidRecordId(string id) => id.Trim().Length is >= 1 and <= 160 && !id.Any(char.IsControl);

    public async Task<IReadOnlyList<SourceDto>> SourcesAsync(Guid actor, CancellationToken ct)
    {
        var grants = await access.ForUserAsync(actor, ct);
        var result = new List<SourceDto>();
        foreach (var item in Catalog)
        {
            var option = options.Value.For(item.Id);
            var configured = !string.IsNullOrWhiteSpace(config.GetConnectionString(IntegrationsOptions.ConnectionKey(item.Id)));
            var allowed = option.AllowedGroupIds.Any(id => grants.Groups.Any(g => g.Id == id));
            var status = !option.Enabled ? "disabled" : option.Transport != "sql" ? "unsupported-transport" : !option.AclContractConfirmed ? "acl-unconfirmed" : !configured ? "unconfigured" : !allowed ? "not-authorized" : "configured";
            var message = status switch { "disabled" => "來源尚未啟用。", "unsupported-transport" => "此來源的 API adapter 尚未實作，請使用 sql 傳輸。", "acl-unconfirmed" => "等待確認來源端的帳號映射與逐筆唯讀授權 view。", "unconfigured" => "等待設定專用的唯讀 SQL 連線。", "not-authorized" => "你的有效群組尚未取得此來源權限。", _ => "已設定。搜尋時才會驗證實際連線及資料權限。" };
            result.Add(new(item.Id, item.Name, item.Description, status, message, status == "configured", item.Kinds));
        }
        return result;
    }

    public async Task<Result<IReadOnlyList<SourceRecordDto>>> SearchAsync(Guid actor, string source, SourceSearchRequest request, CancellationToken ct)
    {
        var item = Catalog.SingleOrDefault(x => x.Id == source);
        if (item.Id is null) return IntegrationErrors.UnknownSource;
        if (request.Query.Trim().Length is < 2 or > 120 || request.Query.Any(char.IsControl) || request.Kind != "all" && !item.Kinds.Contains(request.Kind))
            return IntegrationErrors.SearchInvalid;
        var target = await AuthorizeAsync(actor, source, ct);
        if (!target.IsSuccess) return target.Error;
        var rows = await SafeAsync(source, () => target.Value.Adapter.SearchAsync(target.Value.Actor, request, target.Value.Options.MaxResults, target.Value.Options.CommandTimeoutSeconds, ct), ct);
        if (rows.Count > target.Value.Options.MaxResults || rows.Any(x => !item.Kinds.Contains(x.Kind) || string.IsNullOrWhiteSpace(x.Id) || x.Id.Length > 160 || string.IsNullOrWhiteSpace(x.Title) || x.Title.Length > 120))
            return IntegrationErrors.ContractInvalid;
        var still = await AuthorizeAsync(actor, source, ct, fresh: true);
        if (!still.IsSuccess) return still.Error;
        db.AuditEvents.Add(new() { OwnerId = actor, Action = "integration.searched", Result = "read-only", DetailsJson = JsonSerializer.Serialize(new { source, count = rows.Count }) });
        await db.SaveChangesAsync(ct);
        return Result<IReadOnlyList<SourceRecordDto>>.Ok(rows);
    }

    public async Task<Result<SourceDetailDto>> ReadAsync(Guid actor, string source, string id, CancellationToken ct)
    {
        if (!ValidRecordId(id)) return IntegrationErrors.RecordIdInvalid;
        var target = await AuthorizeAsync(actor, source, ct);
        if (!target.IsSuccess) return target.Error;
        var row = await SafeAsync(source, () => target.Value.Adapter.ReadAsync(target.Value.Actor, id, target.Value.Options.CommandTimeoutSeconds, ct), ct);
        if (row is null) return IntegrationErrors.RecordMissing;
        if (row.Body.Length > 16000 || row.Record.Title.Length is < 1 or > 120 || row.Record.Revision.Length is < 1 or > 160 || row.Record.Id != id || row.SourceId != source || !Catalog.Single(x => x.Id == source).Kinds.Contains(row.Record.Kind))
            return IntegrationErrors.ContractInvalid;
        var still = await AuthorizeAsync(actor, source, ct, fresh: true);
        if (!still.IsSuccess) return still.Error;
        db.AuditEvents.Add(new() { OwnerId = actor, Action = "integration.record.read", Result = "read-only", DetailsJson = JsonSerializer.Serialize(new { source, externalId = id, row.Record.Revision }) });
        await db.SaveChangesAsync(ct);
        return row;
    }

    // A re-check after the source call is fresh: grants read earlier in this request may have been revoked meanwhile.
    private async Task<Result<Target>> AuthorizeAsync(Guid actor, string source, CancellationToken ct, bool fresh = false)
    {
        if (!Catalog.Any(x => x.Id == source)) return IntegrationErrors.UnknownSource;
        if (fresh) access.Invalidate();
        var grants = await access.ForUserAsync(actor, ct);
        if (!grants.Features.Any(x => x.Id == FeatureIds.Integrations)) return IntegrationErrors.FeatureRevoked;
        var value = options.Value.For(source);
        if (value.Transport != "sql") return IntegrationErrors.TransportUnsupported;
        if (!value.Enabled || !value.AclContractConfirmed || string.IsNullOrWhiteSpace(config.GetConnectionString(IntegrationsOptions.ConnectionKey(source)))) return IntegrationErrors.NotConfigured;
        if (!value.AllowedGroupIds.Any(id => grants.Groups.Any(g => g.Id == id))) return IntegrationErrors.SourceForbidden;
        var user = await db.Users.AsNoTracking().SingleAsync(x => x.Id == actor, ct);
        if (string.IsNullOrWhiteSpace(user.Sid) || string.IsNullOrWhiteSpace(user.Account)) return IntegrationErrors.IdentityMissing;
        return new Target(adapters.Single(x => x.Id == source), value, new(user.Sid, user.Account));
    }

    // Connectivity failures of the external system are infrastructure faults, not expected outcomes, so they stay exceptions.
    private async Task<T> SafeAsync<T>(string source, Func<Task<T>> work, CancellationToken ct)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?> { ["ExternalService"] = source });
        try { return await work(); }
        catch (DbException ex) { throw new ApiException(503, "source_unavailable", "唯讀來源目前無法查詢，請確認連線、帳號權限與授權 view。", ex); }
        catch (OperationCanceledException ex) { ct.ThrowIfCancellationRequested(); throw new ApiException(504, "source_timeout", "來源查詢逾時，請縮小搜尋範圍。", ex); }
    }
}
