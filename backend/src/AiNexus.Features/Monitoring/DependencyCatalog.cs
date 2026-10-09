using System.Data.Common;
using System.Diagnostics;
using Microsoft.Data.SqlClient;

namespace AiNexus.Features.Monitoring;

public sealed class DependencyCatalog
{
    private readonly List<(string Host, int Port, string Id)> http = [];
    private readonly List<(string Server, string Database, string Id)> sql = [];
    public DependencyCatalog(IConfiguration config, RuntimeTraffic traffic)
    {
        AddHttp("https://generativelanguage.googleapis.com", "google", "Google AI");
        AddHttp(config["Inference:Providers:Ollama:Endpoint"], "ollama", "Ollama");
        AddHttp(config["Knowledge:Embedding:Endpoint"], "embedding", "向量模型服務");
        AddHttp(config["Knowledge:Rerank:Endpoint"], "rerank", "重排序服務");
        AddHttp(config["Tools:WebSearch:Endpoint"], "search", "連網搜尋");
        AddHttp(config["Integrations:Connectors:Gitea:BaseUrl"], "gitea", "Gitea 程式庫");
        foreach (var (key, id, name) in new[] { ("Nexus", "sql.nexus", "主資料庫"), ("LegacyGdweb", "sql.gdweb", "公文資料庫"), ("LegacyMeiho", "sql.meiho", "校務資料庫") })
        {
            var connection = config.GetConnectionString(key);
            if (string.IsNullOrWhiteSpace(connection)) continue;
            try
            {
                var parsed = new SqlConnectionStringBuilder(connection);
                sql.Add((parsed.DataSource, parsed.InitialCatalog, id)); traffic.RegisterDependency(id, name, "database");
            }
            catch (ArgumentException) { /* Startup/storage validation owns malformed connection settings. */ }
        }
        traffic.RegisterDependency("http.other", "其他 HTTP 服務", "http");
        traffic.RegisterDependency("sql.other", "其他 SQL 資料庫", "database");
        void AddHttp(string? value, string id, string name)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return;
            // Shared model endpoints represent one transport dependency, without counting each model as a server.
            if (http.Any(x => x.Host.Equals(uri.Host, StringComparison.OrdinalIgnoreCase) && x.Port == uri.Port)) return;
            http.Add((uri.Host, uri.Port, id)); traffic.RegisterDependency(id, name, "http");
        }
    }
    public string Http(Uri? uri) => uri is null ? "http.other" : http.FirstOrDefault(x => x.Host.Equals(uri.Host, StringComparison.OrdinalIgnoreCase) && x.Port == uri.Port).Id ?? "http.other";
    public string Sql(DbConnection? connection) => sql.FirstOrDefault(x => x.Server.Equals(connection?.DataSource, StringComparison.OrdinalIgnoreCase) && x.Database.Equals(connection?.Database, StringComparison.OrdinalIgnoreCase)).Id ?? "sql.other";
}
