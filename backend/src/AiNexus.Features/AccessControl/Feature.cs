namespace AiNexus.Features.AccessControl;

public sealed class Feature
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Route { get; set; } = "";
    public int SortOrder { get; set; }
    public bool Enabled { get; set; } = true;
}
