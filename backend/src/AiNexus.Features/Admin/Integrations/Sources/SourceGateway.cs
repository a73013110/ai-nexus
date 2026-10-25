using AiNexus.Data.App;
using System.Data.Common;
using System.Text.Json;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity.Authentication;
using AiNexus.Features.Admin.Identity.Users;
using AiNexus.Platform.Data;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.Integrations.Sources;

/// <summary>
/// 讀取舊系統唯讀來源的唯一入口。每次查詢前後都檢查授權，查詢期間被撤銷的授權不會讓資料外流；每次讀取都寫入稽核。
/// </summary>
internal sealed class SourceGateway(AppDbContext db, AccessService access, IOptions<IntegrationsOptions> options, IOptions<DatabasesOptions> databases,
    IOptions<AdAuthenticationOptions> directory, IEnumerable<IControlledSourceAdapter> adapters, ILogger<SourceGateway> logger)
{
    private static readonly SourceEntry[] Catalog = [
        new("gd", "公文系統", "查詢授權文件、狀態、版本與簽核歷程。", ["document"], GdDatabase.Name),
        new("meiho", "校務系統", "先提供核准的規範與單位資料；學生資料須另建逐筆授權 adapter。", ["reference", "organization"], MeihoDatabase.Name)
    ];

    private sealed record SourceEntry(string Id, string Name, string Description, string[] Kinds, string Database);

    private sealed record Target(SourceEntry Entry, IControlledSourceAdapter Adapter, SourceOptions Options, SourceActor Actor);

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
            var status = SetupStatus(option, configured);
            if (status == "configured" && !Admits(option, grants)) status = "not-authorized";
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
        if (Find(source) is not { } item) return IntegrationsErrors.UnknownSource;
        // 查詢字串與類別都是純計算，先算出來再判斷，不影響結果。
        var queryInvalid = request.Query.Trim().Length is < 2 or > 120 || request.Query.Any(char.IsControl);
        var kindInvalid = request.Kind != "all" && !item.Kinds.Contains(request.Kind);
        if (queryInvalid || kindInvalid) return IntegrationsErrors.SearchInvalid;
        var target = await AuthorizeAsync(actor, source, ct);
        if (!target.IsSuccess) return target.Error;
        var (_, adapter, settings, sourceActor) = target.Value;
        var rows = await SafeAsync(source, () => adapter.SearchAsync(sourceActor, request, settings.MaxResults, settings.CommandTimeoutSeconds, ct), ct);
        if (rows.Count > settings.MaxResults
            || rows.Any(x => !item.Kinds.Contains(x.Kind) || string.IsNullOrWhiteSpace(x.Id) || x.Id.Length > 160
                || string.IsNullOrWhiteSpace(x.Title) || x.Title.Length > 120))
            return IntegrationsErrors.ContractInvalid;
        var still = await AuthorizeAsync(actor, source, ct, fresh: true);
        if (!still.IsSuccess) return still.Error;
        await AuditAsync(actor, "integration.searched", new { source, count = rows.Count }, ct);
        return Result<IReadOnlyList<SourceRecordDto>>.Ok(rows);
    }

    public async Task<Result<SourceDetailDto>> ReadAsync(Guid actor, string source, string id, CancellationToken ct)
    {
        if (!ValidRecordId(id)) return IntegrationsErrors.RecordIdInvalid;
        var target = await AuthorizeAsync(actor, source, ct);
        if (!target.IsSuccess) return target.Error;
        var (entry, adapter, settings, sourceActor) = target.Value;
        var read = await SafeAsync(source, () => adapter.ReadAsync(sourceActor, id, settings.CommandTimeoutSeconds, ct), ct);
        if (!read.IsSuccess) return read.Error;
        var row = read.Value;
        if (row.Body.Length > 16000
            || row.Record.Title.Length is < 1 or > 120
            || row.Record.Revision.Length is < 1 or > 160
            || row.Record.Id != id
            || row.SourceId != source
            || !entry.Kinds.Contains(row.Record.Kind))
            return IntegrationsErrors.ContractInvalid;
        var still = await AuthorizeAsync(actor, source, ct, fresh: true);
        if (!still.IsSuccess) return still.Error;
        await AuditAsync(actor, "integration.record.read", new { source, externalId = id, row.Record.Revision }, ct);
        return row;
    }

    /// <summary>讀取紀錄並確認仍是使用者看到的版本；帶入對話與匯入快照都只接受畫面上確認過的那一版。</summary>
    public async Task<Result<SourceDetailDto>> ReadAtRevisionAsync(Guid actor, string source, string id, string expectedRevision, CancellationToken ct)
    {
        var read = await ReadAsync(actor, source, id, ct);
        if (!read.IsSuccess) return read.Error;
        return read.Value.Record.Revision == expectedRevision ? read.Value : IntegrationsErrors.Changed;
    }

    private static SourceEntry? Find(string source) => Catalog.SingleOrDefault(x => x.Id == source);

    // 空的允許清單不授權任何人，平台管理員也不例外。
    private static bool Admits(SourceOptions option, AccessDto grants) => option.AllowedGroupIds.Any(id => grants.Groups.Any(g => g.Id == id));

    private async Task AuditAsync(Guid actor, string action, object details, CancellationToken ct)
    {
        db.AuditEvents.Add(new() { OwnerId = actor, Action = action, Result = "read-only", DetailsJson = JsonSerializer.Serialize(details) });
        await db.SaveChangesAsync(ct);
    }

    // 來源查詢後的第二次授權檢查必須 fresh（捨棄快取）：本次請求稍早讀到的授權可能已在查詢期間被撤銷。
    private async Task<Result<Target>> AuthorizeAsync(Guid actor, string source, CancellationToken ct, bool fresh = false)
    {
        if (Find(source) is not { } item) return IntegrationsErrors.UnknownSource;
        if (fresh) access.Invalidate();
        var grants = await access.ForUserAsync(actor, ct);
        if (!grants.Features.Any(x => x.Id == FeatureCatalog.Integrations)) return AccessControlErrors.FeatureForbidden;
        var value = options.Value.For(source);
        if (value.Transport != "sql") return IntegrationsErrors.TransportUnsupported;
        if (!value.Enabled || !value.AclContractConfirmed || !databases.Value.For(item.Database).Configured) return IntegrationsErrors.NotConfigured;
        if (!Admits(value, grants)) return IntegrationsErrors.SourceForbidden;
        // 外部 view 以 AD SID＋完整帳號（帳號@網域）授權：只有本地帳號、或還沒以 SID 綁定 AD 的使用者沒有可用的身分。
        var user = await db.Users.AsNoTracking().SingleAsync(x => x.Id == actor, ct);
        if (!UserAccounts.DirectoryBound(user) || UserAccounts.DirectoryAccount(user, directory.Value.Domain) is not { } account) return IntegrationsErrors.IdentityMissing;
        return new Target(item, adapters.Single(x => x.Id == source), value, new(user.Sid, account));
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
