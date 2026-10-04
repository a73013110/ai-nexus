using System.Data;
using AiNexus.BuildingBlocks;
using AiNexus.Database;
using EDoc.Core.Database.Interfaces;
using EDoc.Core.Database.Markers;
using EDoc.Core.Database.Enums;

namespace AiNexus.Modules.Integrations;

public sealed class IntegrationsOptions
{
    public SourceOptions Gdweb { get; set; } = new();
    public SourceOptions Meiho { get; set; } = new();
    public SourceOptions For(string id) => id switch { "gdweb" => Gdweb, "meiho" => Meiho, _ => throw new ApiException(404, "source_unknown", "找不到此資料來源。") };
}
public sealed class SourceOptions
{
    public string Transport { get; set; } = "sql";
    public bool Enabled { get; set; }
    public bool AclContractConfirmed { get; set; }
    public string[] AllowedGroupIds { get; set; } = [];
    public int CommandTimeoutSeconds { get; set; } = 10;
    public int MaxResults { get; set; } = 30;
}
public interface ILegacyGdwebDatabase : IDbMarker
{
    static string IDbMarker.ConnectionStringKey => "LegacyGdweb";
    static DbProviderType IDbMarker.ProviderType => DbProviderType.SqlServer;
}
public interface ILegacyMeihoDatabase : IDbMarker
{
    static string IDbMarker.ConnectionStringKey => "LegacyMeiho";
    static DbProviderType IDbMarker.ProviderType => DbProviderType.SqlServer;
}
public sealed record SourceActor(string Sid, string Account);
public sealed record SourceSearchRequest(string Query, string Kind = "all");
public sealed record SourceDto(string Id, string Name, string Description, string Status, string Notice, bool CanQuery, IReadOnlyList<string> Kinds);
public sealed record SourceRecordDto(string Id, string Kind, string Title, string Status, string Revision, DateTimeOffset ModifiedAt);
public sealed record SourceHistoryDto(DateTimeOffset At, string Kind, string Actor, string Description, string Revision);
public sealed record SourceDetailDto(string SourceId, SourceRecordDto Record, string Body, IReadOnlyList<SourceHistoryDto> History, bool HistoryLimited);
public sealed class SourceRow
{
    public string RecordId { get; set; } = "";
    public string RecordKind { get; set; } = "";
    public string Title { get; set; } = "";
    public string Status { get; set; } = "";
    public string Revision { get; set; } = "";
    public DateTimeOffset ModifiedAt { get; set; }
    public string? Body { get; set; }
    public SourceRecordDto Describe() => new(RecordId, RecordKind, Title, Status, Revision, ModifiedAt);
}
public interface IControlledSourceAdapter
{
    string Id { get; }
    Task<IReadOnlyList<SourceRecordDto>> SearchAsync(SourceActor actor, SourceSearchRequest request, int take, int timeout, CancellationToken ct);
    Task<SourceDetailDto?> ReadAsync(SourceActor actor, string id, int timeout, CancellationToken ct);
}

// SQL is fixed in code. Only the source-side authorized views are readable by the SQL login.
// Both full AD account and SID must match; account suffixes are never used as identities.
public abstract class AuthorizedSqlSource<TDb>(IDbHelper<TDb> db) : IControlledSourceAdapter where TDb : IDbMarker
{
    public abstract string Id { get; }
    private const string SearchSql = """
        SELECT TOP (@Take) RecordId, RecordKind, Title, Status, Revision, ModifiedAt
        FROM nexus.AuthorizedRecords
        WHERE ActorSid = @ActorSid AND ActorAccount = @ActorAccount
          AND (@Kind = 'all' OR RecordKind = @Kind)
          AND (Title LIKE @Pattern ESCAPE '~' OR RecordId LIKE @Pattern ESCAPE '~')
        ORDER BY ModifiedAt DESC, RecordId
        """;
    private const string ReadSql = """
        SELECT RecordId, RecordKind, Title, Status, Revision, ModifiedAt,
          CASE WHEN DATALENGTH(Body) <= 32000 THEN Body ELSE NULL END AS Body
        FROM nexus.AuthorizedRecords
        WHERE ActorSid = @ActorSid AND ActorAccount = @ActorAccount AND RecordId = @RecordId
        """;
    private const string HistorySql = """
        SELECT TOP (101) At, Kind, Actor, Description, Revision
        FROM nexus.AuthorizedRecordHistory
        WHERE ActorSid = @ActorSid AND ActorAccount = @ActorAccount AND RecordId = @RecordId
        ORDER BY At DESC, EventId DESC
        """;
    public async Task<IReadOnlyList<SourceRecordDto>> SearchAsync(SourceActor actor, SourceSearchRequest request, int take, int timeout, CancellationToken ct)
    {
        var pattern = "%" + request.Query.Trim().Replace("~", "~~").Replace("%", "~%").Replace("_", "~_").Replace("[", "~[") + "%";
        var rows = await db.QueryAsync<SourceRow>(SearchSql, new { ActorSid = actor.Sid, ActorAccount = actor.Account, Kind = request.Kind, Pattern = pattern, Take = take }, commandTimeout: timeout, commandType: CommandType.Text, cancellationToken: ct);
        return rows.Select(x => x.Describe()).ToArray();
    }
    public async Task<SourceDetailDto?> ReadAsync(SourceActor actor, string id, int timeout, CancellationToken ct)
    {
        var args = new { ActorSid = actor.Sid, ActorAccount = actor.Account, RecordId = id };
        var row = await db.QuerySingleOrDefaultAsync<SourceRow>(ReadSql, args, commandTimeout: timeout, commandType: CommandType.Text, cancellationToken: ct);
        if (row is null) return null;
        if (row.Body is null) throw new ApiException(409, "source_body_too_large", "來源內容未公開或超過 16,000 字元，請至原系統閱讀完整內容。");
        var history = (await db.QueryAsync<SourceHistoryDto>(HistorySql, args, commandTimeout: timeout, commandType: CommandType.Text, cancellationToken: ct)).ToArray();
        // Recheck the primary grant after retrieving child history as revocation can happen between calls.
        var confirmed = await db.QuerySingleOrDefaultAsync<SourceRow>(ReadSql, args, commandTimeout: timeout, commandType: CommandType.Text, cancellationToken: ct);
        if (confirmed is null) return null;
        if (confirmed.Revision != row.Revision) throw new ApiException(409, "source_changed", "來源版本剛更新，請重新載入後再使用。");
        return new(Id, row.Describe(), row.Body, history.Take(100).ToArray(), history.Length > 100);
    }
}
public sealed class GdwebSource(IDbHelper<ILegacyGdwebDatabase> db) : AuthorizedSqlSource<ILegacyGdwebDatabase>(db) { public override string Id => "gdweb"; }
public sealed class MeihoSource(IDbHelper<ILegacyMeihoDatabase> db) : AuthorizedSqlSource<ILegacyMeihoDatabase>(db) { public override string Id => "meiho"; }
