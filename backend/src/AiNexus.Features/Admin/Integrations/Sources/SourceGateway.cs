using AiNexus.Data.App;
using System.Data.Common;
using System.Text.Json;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Data;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.Integrations.Sources;

/// <summary>
/// 讀取舊系統唯讀來源的唯一入口。每次查詢前後都檢查授權，查詢期間被撤銷的授權不會讓資料外流；每次讀取都寫入稽核。
/// </summary>
internal sealed class SourceGateway(AppDbContext db, AccessService access, IOptions<IntegrationsOptions> options, IOptions<DatabasesOptions> databases,
    IEnumerable<IControlledSourceAdapter> adapters, ILogger<SourceGateway> logger)
{
    private static readonly (string Id, string Name, string Description, string[] Kinds, string Database)[] Catalog = [
        ("gdweb", "公文系統", "查詢授權文件、狀態、版本與簽核歷程。", ["document"], GdwebDatabase.Name),
        ("meiho", "校務系統", "先提供核准的規範與單位資料；學生資料須另建逐筆授權 adapter。", ["reference", "organization"], MeihoDatabase.Name)
    ];

    private sealed record Target(IControlledSourceAdapter Adapter, SourceOptions Options, SourceActor Actor);

    /// <summary>目錄中的來源與其 <c>Databases</c> 設定；不含使用者權限，僅供列舉與健康判定。</summary>
    public static IEnumerable<(string Id, string Name, string Database)> Sources => Catalog.Select(x => (x.Id, x.Name, x.Database));

    /// <summary>在任何使用者授權之前，判斷來源的設定進度（停用、傳輸不支援、ACL 未確認、未設定連線、已設定）。</summary>
    public static string SetupStatus(SourceOptions option, bool connectionConfigured) =>
        !option.Enabled ? "disabled"
        : option.Transport != "sql" ? "unsupported-transport"
        : !option.AclContractConfirmed ? "acl-unconfirmed"
        : !connectionConfigured ? "unconfigured"
        : "configured";

    public static bool ValidRecordId(string id) => id.Trim().Length is >= 1 and <= 160 && !id.Any(char.IsControl);

    public async Task<IReadOnlyList<SourceDto>> SourcesAsync(Guid actor, CancellationToken ct)
    {
        var grants = await access.ForUserAsync(actor, ct);
        var result = new List<SourceDto>();
        foreach (var item in Catalog)
        {
            var option = options.Value.For(item.Id);
            var configured = databases.Value.For(item.Database).Configured;
            var allowed = option.AllowedGroupIds.Any(id => grants.Groups.Any(g => g.Id == id));
            var status = SetupStatus(option, configured);
            if (status == "configured" && !allowed) status = "not-authorized";
            var message = status switch
            {
                "disabled" => "來源尚未啟用。",
                "unsupported-transport" => "此來源的 API adapter 尚未實作，請使用 sql 傳輸。",
                "acl-unconfirmed" => "等待確認來源端的帳號映射與逐筆唯讀授權 view。",
                "unconfigured" => "等待設定專用的唯讀 SQL 連線。",
                "not-authorized" => "你的有效群組尚未取得此來源權限。",
                _ => "已設定。搜尋時才會驗證實際連線及資料權限。",
            };
            result.Add(new(item.Id, item.Name, item.Description, status, message, status == "configured", item.Kinds));
        }
        return result;
    }

    public async Task<Result<IReadOnlyList<SourceRecordDto>>> SearchAsync(Guid actor, string source, SourceSearchRequest request, CancellationToken ct)
    {
        var item = Catalog.SingleOrDefault(x => x.Id == source);
        if (item.Id is null) return IntegrationsErrors.UnknownSource;
        // 查詢字串與類別都是純計算，先算出來再判斷，不影響結果。
        var queryInvalid = request.Query.Trim().Length is < 2 or > 120 || request.Query.Any(char.IsControl);
        var kindInvalid = request.Kind != "all" && !item.Kinds.Contains(request.Kind);
        if (queryInvalid || kindInvalid) return IntegrationsErrors.SearchInvalid;
        var target = await AuthorizeAsync(actor, source, ct);
        if (!target.IsSuccess) return target.Error;
        var rows = await SafeAsync(source, () => target.Value.Adapter.SearchAsync(target.Value.Actor, request, target.Value.Options.MaxResults, target.Value.Options.CommandTimeoutSeconds, ct), ct);
        if (rows.Count > target.Value.Options.MaxResults
            || rows.Any(x => !item.Kinds.Contains(x.Kind) || string.IsNullOrWhiteSpace(x.Id) || x.Id.Length > 160
                || string.IsNullOrWhiteSpace(x.Title) || x.Title.Length > 120))
            return IntegrationsErrors.ContractInvalid;
        var still = await AuthorizeAsync(actor, source, ct, fresh: true);
        if (!still.IsSuccess) return still.Error;
        db.AuditEvents.Add(new() { OwnerId = actor, Action = "integration.searched", Result = "read-only", DetailsJson = JsonSerializer.Serialize(new { source, count = rows.Count }) });
        await db.SaveChangesAsync(ct);
        return Result<IReadOnlyList<SourceRecordDto>>.Ok(rows);
    }

    public async Task<Result<SourceDetailDto>> ReadAsync(Guid actor, string source, string id, CancellationToken ct)
    {
        if (!ValidRecordId(id)) return IntegrationsErrors.RecordIdInvalid;
        var target = await AuthorizeAsync(actor, source, ct);
        if (!target.IsSuccess) return target.Error;
        var read = await SafeAsync(source, () => target.Value.Adapter.ReadAsync(target.Value.Actor, id, target.Value.Options.CommandTimeoutSeconds, ct), ct);
        if (!read.IsSuccess) return read.Error;
        var row = read.Value;
        if (row.Body.Length > 16000
            || row.Record.Title.Length is < 1 or > 120
            || row.Record.Revision.Length is < 1 or > 160
            || row.Record.Id != id
            || row.SourceId != source
            || !Catalog.Single(x => x.Id == source).Kinds.Contains(row.Record.Kind))
            return IntegrationsErrors.ContractInvalid;
        var still = await AuthorizeAsync(actor, source, ct, fresh: true);
        if (!still.IsSuccess) return still.Error;
        db.AuditEvents.Add(new() { OwnerId = actor, Action = "integration.record.read", Result = "read-only", DetailsJson = JsonSerializer.Serialize(new { source, externalId = id, row.Record.Revision }) });
        await db.SaveChangesAsync(ct);
        return row;
    }

    // 來源查詢後的第二次授權檢查必須 fresh（捨棄快取）：本次請求稍早讀到的授權可能已在查詢期間被撤銷。
    private async Task<Result<Target>> AuthorizeAsync(Guid actor, string source, CancellationToken ct, bool fresh = false)
    {
        var item = Catalog.SingleOrDefault(x => x.Id == source);
        if (item.Id is null) return IntegrationsErrors.UnknownSource;
        if (fresh) access.Invalidate();
        var grants = await access.ForUserAsync(actor, ct);
        if (!grants.Features.Any(x => x.Id == FeatureIds.Integrations)) return AccessControlErrors.FeatureForbidden;
        var value = options.Value.For(source);
        if (value.Transport != "sql") return IntegrationsErrors.TransportUnsupported;
        if (!value.Enabled || !value.AclContractConfirmed || !databases.Value.For(item.Database).Configured) return IntegrationsErrors.NotConfigured;
        if (!value.AllowedGroupIds.Any(id => grants.Groups.Any(g => g.Id == id))) return IntegrationsErrors.SourceForbidden;
        var user = await db.Users.AsNoTracking().SingleAsync(x => x.Id == actor, ct);
        if (string.IsNullOrWhiteSpace(user.Sid) || string.IsNullOrWhiteSpace(user.Account)) return IntegrationsErrors.IdentityMissing;
        return new Target(adapters.Single(x => x.Id == source), value, new(user.Sid, user.Account));
    }

    // 外部系統的連線失敗屬於基礎設施故障而非預期結果，因此維持拋出例外，不轉成 Result。
    private async Task<T> SafeAsync<T>(string source, Func<Task<T>> work, CancellationToken ct)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?> { ["ExternalService"] = source });
        try { return await work(); }
        catch (DbException ex) { throw new ExternalServiceException(Error.Unavailable("source_unavailable"), "唯讀來源目前無法查詢，請確認連線、帳號權限與授權 view。", ex); }
        catch (OperationCanceledException ex) { ct.ThrowIfCancellationRequested(); throw new ExternalServiceException(Error.Timeout("source_timeout"), "來源查詢逾時，請縮小搜尋範圍。", ex); }
    }
}
