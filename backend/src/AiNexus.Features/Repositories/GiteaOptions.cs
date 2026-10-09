namespace AiNexus.Features.Repositories;

public sealed class GiteaOptions
{
    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "https://gitea.hanglong.com.tw/";
    public int TimeoutSeconds { get; set; } = 10;
    public int MaxFileBytes { get; set; } = 200000;
}
