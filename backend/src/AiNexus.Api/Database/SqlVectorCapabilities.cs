using System.Text.Json;
using AiNexus.BuildingBlocks;
using EDoc.Core.Database.Interfaces;

namespace AiNexus.Database;

public sealed record SqlVectorCapabilitiesDto(string Version, string Edition, int MajorVersion, bool NativeVector, bool ExactDistance);
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
        return new(info.Version, info.Edition, info.MajorVersion, available, available);
    }
    public async Task VerifyAsync(string output, CancellationToken ct)
    {
        var result = await ReadAsync(ct);
        await File.WriteAllTextAsync(output, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }), ct);
        Console.WriteLine($"SQL {result.Version} · {result.Edition} · native VECTOR / exact cosine distance: {(result.ExactDistance ? "PASS" : "unavailable; portable retrieval required")}");
    }
    private sealed class ServerInfo
    {
        public string Version { get; set; } = "";
        public string Edition { get; set; } = "";
        public int MajorVersion { get; set; }
    }
}
