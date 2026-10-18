using AiNexus.Platform.Data.Sql;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Integrations.Sources;

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

// SQL 寫死在程式碼中，不接受外部輸入組成查詢；SQL 登入只能讀取來源端的授權 view。
// AD 帳號與 SID 必須同時相符，且不得以帳號後綴做為身分識別。
public abstract class AuthorizedSqlSource<TDatabase>(ISqlDatabase<TDatabase> db) : IControlledSourceAdapter
{
    public abstract string Id { get; }
    private const string SearchSql = """
        SELECT TOP (@Take) RecordId, RecordKind, Title, Status, Revision, ModifiedAt
        FROM ainexus.AuthorizedRecords
        WHERE ActorSid = @ActorSid AND ActorAccount = @ActorAccount
          AND (@Kind = 'all' OR RecordKind = @Kind)
          AND (Title LIKE @Pattern ESCAPE '~' OR RecordId LIKE @Pattern ESCAPE '~')
        ORDER BY ModifiedAt DESC, RecordId
        """;
    private const string ReadSql = """
        SELECT RecordId, RecordKind, Title, Status, Revision, ModifiedAt,
          CASE WHEN DATALENGTH(Body) <= 32000 THEN Body ELSE NULL END AS Body
        FROM ainexus.AuthorizedRecords
        WHERE ActorSid = @ActorSid AND ActorAccount = @ActorAccount AND RecordId = @RecordId
        """;
    private const string HistorySql = """
        SELECT TOP (101) At, Kind, Actor, Description, Revision
        FROM ainexus.AuthorizedRecordHistory
        WHERE ActorSid = @ActorSid AND ActorAccount = @ActorAccount AND RecordId = @RecordId
        ORDER BY At DESC, EventId DESC
        """;
    public async Task<IReadOnlyList<SourceRecordDto>> SearchAsync(SourceActor actor, SourceSearchRequest request, int take, int timeout, CancellationToken ct)
    {
        var pattern = "%" + request.Query.Trim().Replace("~", "~~").Replace("%", "~%").Replace("_", "~_").Replace("[", "~[") + "%";
        var args = new { ActorSid = actor.Sid, ActorAccount = actor.Account, Kind = request.Kind, Pattern = pattern, Take = take };
        var rows = await db.QueryAsync<SourceRow>(SearchSql, args, commandTimeout: timeout, cancellationToken: ct);
        return rows.Select(x => x.Describe()).ToArray();
    }
    public async Task<Result<SourceDetailDto>> ReadAsync(SourceActor actor, string id, int timeout, CancellationToken ct)
    {
        var args = new { ActorSid = actor.Sid, ActorAccount = actor.Account, RecordId = id };
        var row = await db.QuerySingleOrDefaultAsync<SourceRow>(ReadSql, args, commandTimeout: timeout, cancellationToken: ct);
        if (row is null) return IntegrationsErrors.RecordMissing;
        if (row.Body is null) return IntegrationsErrors.BodyTooLarge;
        var history = (await db.QueryAsync<SourceHistoryDto>(HistorySql, args, commandTimeout: timeout, cancellationToken: ct)).ToArray();
        // 取得歷史後再查一次主紀錄：授權可能在兩次查詢之間被撤銷，也可能紀錄已被改版（Revision 不符即視為已變更）。
        var confirmed = await db.QuerySingleOrDefaultAsync<SourceRow>(ReadSql, args, commandTimeout: timeout, cancellationToken: ct);
        if (confirmed is null) return IntegrationsErrors.RecordMissing;
        if (confirmed.Revision != row.Revision) return IntegrationsErrors.Changed;
        return new SourceDetailDto(Id, row.Describe(), row.Body, history.Take(100).ToArray(), history.Length > 100);
    }
}
