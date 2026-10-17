using System.Text.Json;
using AiNexus.Platform.Data.Sql;

namespace AiNexus.Features.Persistence;

/// <summary>SQL Server 向量與全文檢索能力的檢測結果。</summary>
public sealed record SqlVectorCapabilitiesDto(
    string Version,
    string Edition,
    int MajorVersion,
    bool NativeVector,
    bool ExactDistance,
    bool FullTextInstalled = false,
    bool TraditionalChineseWordBreaker = false,
    bool FullTextIndex = false);

/// <summary>
/// SQL Server 向量推論與全文檢索能力檢測器。
/// </summary>
/// <remarks>
/// 檢測項目與版本依賴：
/// 1. 原生向量與餘弦距離：先檢查主版本號是否 ≥ 17（SQL Server 2025），再實際執行
///    <c>VECTOR_DISTANCE('cosine', @v, @v)</c>。版本號相符不代表功能可用，所以要真的呼叫一次。
/// 2. 全文搜尋服務：查詢 <c>SERVERPROPERTY('IsFullTextInstalled')</c> 確認全文元件已安裝。
/// 3. 繁體中文斷詞器：檢查系統是否包含 LCID 1028 (繁體中文) 之全文斷詞器，確保知識庫檢索斷詞精準度。
/// 4. 知識庫全文索引：檢查 <c>knowledge.Chunks</c> 資料表之全文索引是否已建立並處於啟用狀態。
/// </remarks>
public sealed class SqlVectorCapabilities(ISqlDatabase<NexusDbContext> sql)
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    /// <summary>
    /// 讀取當前資料庫執行個體的版本、原生向量支援能力與全文檢索組態。
    /// </summary>
    public async Task<SqlVectorCapabilitiesDto> ReadAsync(CancellationToken ct)
    {
        var info = await sql.QuerySingleAsync<ServerInfo>(
            "SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ProductVersion')) AS Version, " +
            "CONVERT(nvarchar(128),SERVERPROPERTY('Edition')) AS Edition, " +
            "CONVERT(int,SERVERPROPERTY('ProductMajorVersion')) AS MajorVersion",
            commandTimeout: 5,
            cancellationToken: ct);

        var available = false;
        if (info.MajorVersion >= 17)
        {
            try
            {
                var distance = await sql.QuerySingleAsync<double>(
                    "DECLARE @v VECTOR(3) = CAST('[1,0,0]' AS VECTOR(3)); " +
                    "SELECT VECTOR_DISTANCE('cosine', @v, @v)",
                    commandTimeout: 5,
                    cancellationToken: ct);

                available = double.IsFinite(distance) && Math.Abs(distance) < .00001;
            }
            catch (Microsoft.Data.SqlClient.SqlException)
            {
                // 不支援或尚未啟用時函式會丟出例外；視為不可用即可，不中斷整體檢測。
            }
        }

        var fulltext = await sql.QuerySingleAsync<FullTextInfo>(
            "SELECT CONVERT(int,SERVERPROPERTY('IsFullTextInstalled')) AS Installed, " +
            "CASE WHEN EXISTS (SELECT 1 FROM sys.fulltext_languages WHERE lcid = 1028) THEN 1 ELSE 0 END AS Chinese, " +
            "CASE WHEN EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('knowledge.Chunks') AND is_enabled = 1) THEN 1 ELSE 0 END AS Indexed",
            commandTimeout: 5,
            cancellationToken: ct);

        return new(
            Version: info.Version,
            Edition: info.Edition,
            MajorVersion: info.MajorVersion,
            NativeVector: available,
            ExactDistance: available,
            FullTextInstalled: fulltext.Installed == 1,
            TraditionalChineseWordBreaker: fulltext.Chinese == 1,
            FullTextIndex: fulltext.Indexed == 1);
    }

    /// <summary>
    /// 驗證向量資料庫特性並將結果輸出至指定 JSON 檔案與控制台。
    /// </summary>
    public async Task VerifyAsync(string output, CancellationToken ct)
    {
        var result = await ReadAsync(ct);
        await File.WriteAllTextAsync(output, JsonSerializer.Serialize(result, Indented), ct);
        Console.WriteLine($"SQL {result.Version} · {result.Edition} · 原生向量／精確距離：{(result.ExactDistance ? "通過" : "無法使用；需要 SQL Server 2025")}");
        Console.WriteLine($"全文元件：{result.FullTextInstalled} · 繁中 1028 斷詞器：{result.TraditionalChineseWordBreaker} · 知識全文索引：{result.FullTextIndex}");
    }
    private sealed class ServerInfo
    {
        public string Version { get; set; } = "";
        public string Edition { get; set; } = "";
        public int MajorVersion { get; set; }
    }
    private sealed class FullTextInfo { public int Installed { get; set; } public int Chinese { get; set; } public int Indexed { get; set; } }
}
