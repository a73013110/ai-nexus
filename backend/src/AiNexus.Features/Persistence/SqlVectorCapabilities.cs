using System.Text.Json;
using EDoc.Core.Database.Interfaces;

namespace AiNexus.Features.Persistence;

public sealed record SqlVectorCapabilitiesDto(string Version, string Edition, int MajorVersion, bool NativeVector, bool ExactDistance,
    bool FullTextInstalled = false, bool TraditionalChineseWordBreaker = false, bool FullTextIndex = false);
public sealed class SqlVectorCapabilities(IDbHelper<INexusDatabase> sql)
{
    public async Task<SqlVectorCapabilitiesDto> ReadAsync(CancellationToken ct)
    {
        var info = await sql.QuerySingleAsync<ServerInfo>("SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ProductVersion')) AS Version, CONVERT(nvarchar(128),SERVERPROPERTY('Edition')) AS Edition, CONVERT(int,SERVERPROPERTY('ProductMajorVersion')) AS MajorVersion", commandTimeout: 5, cancellationToken: ct);
        var available = false;
        if (info.MajorVersion >= 17)
        {
            try
            {
                var distance = await sql.QuerySingleAsync<double>("DECLARE @v VECTOR(3) = CAST('[1,0,0]' AS VECTOR(3)); SELECT VECTOR_DISTANCE('cosine', @v, @v)", commandTimeout: 5, cancellationToken: ct);
                available = double.IsFinite(distance) && Math.Abs(distance) < .00001;
            }
            catch (Microsoft.Data.SqlClient.SqlException) { }
        }
        var fulltext = await sql.QuerySingleAsync<FullTextInfo>("SELECT CONVERT(int,SERVERPROPERTY('IsFullTextInstalled')) AS Installed, CASE WHEN EXISTS (SELECT 1 FROM sys.fulltext_languages WHERE lcid = 1028) THEN 1 ELSE 0 END AS Chinese, CASE WHEN EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('knowledge.Chunks') AND is_enabled = 1) THEN 1 ELSE 0 END AS Indexed", commandTimeout: 5, cancellationToken: ct);
        return new(info.Version, info.Edition, info.MajorVersion, available, available, fulltext.Installed == 1, fulltext.Chinese == 1, fulltext.Indexed == 1);
    }
    public async Task VerifyAsync(string output, CancellationToken ct)
    {
        var result = await ReadAsync(ct);
        await File.WriteAllTextAsync(output, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }), ct);
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
