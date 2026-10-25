using System.Data.Common;
using AiNexus.Platform.Data;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.Monitoring.Traffic;

public sealed class DependencyCatalog
{
    private readonly List<(string Host, int Port, string Id)> http = [];
    private readonly List<(string Server, string Database, string Id)> sql = [];

    public DependencyCatalog(IConfiguration config, IOptions<DatabasesOptions> databases, RuntimeTraffic traffic)
    {
        AddHttp("https://generativelanguage.googleapis.com", "google", "Google AI");
        AddHttp(config["Inference:Providers:Ollama:Endpoint"], "ollama", "Ollama");
        AddHttp(config["Knowledge:Embedding:Endpoint"], "embedding", "向量模型服務");
        AddHttp(config["Knowledge:Rerank:Endpoint"], "rerank", "重排序服務");
        AddHttp(config["WebSearch:Endpoint"], "search", "連網搜尋");
        AddHttp(config["Repositories:Gitea:BaseUrl"], "gitea", "Gitea 程式庫");
        foreach (var (name, database) in databases.Value.Where(x => x.Value.Configured).OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            var id = "sql." + name.ToLowerInvariant();
            sql.Add((database.Server, database.Name, id));
            traffic.RegisterDependency(id, databases.Value.Label(name), "database");
        }
        traffic.RegisterDependency("http.other", "其他 HTTP 服務", "http");
        traffic.RegisterDependency("sql.other", "其他 SQL 資料庫", "database");
        void AddHttp(string? value, string id, string name)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return;
            // 同一端點上的多個模型共用一條傳輸相依，不應把每個模型都計為一台伺服器。
            if (http.Any(x => x.Host.Equals(uri.Host, StringComparison.OrdinalIgnoreCase) && x.Port == uri.Port)) return;
            http.Add((uri.Host, uri.Port, id)); traffic.RegisterDependency(id, name, "http");
        }
    }
    public string Http(Uri? uri) => uri is null ? "http.other" : http.FirstOrDefault(x => x.Host.Equals(uri.Host, StringComparison.OrdinalIgnoreCase) && x.Port == uri.Port).Id ?? "http.other";
    public string Sql(DbConnection? connection) => sql.FirstOrDefault(x => x.Server.Equals(connection?.DataSource, StringComparison.OrdinalIgnoreCase) && x.Database.Equals(connection?.Database, StringComparison.OrdinalIgnoreCase)).Id ?? "sql.other";
}
