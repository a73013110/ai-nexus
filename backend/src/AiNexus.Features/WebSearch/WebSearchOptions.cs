namespace AiNexus.Features.WebSearch;

public sealed class WebSearchOptions
{
    public bool Enabled { get; set; }
    public string Provider { get; set; } = "searxng";
    public string Endpoint { get; set; } = "http://localhost:8080/";
    public string ApiKey { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 10;
    public int MaxResults { get; set; } = 5;
    public int MaxDailyRequests { get; set; } = 100;
}
