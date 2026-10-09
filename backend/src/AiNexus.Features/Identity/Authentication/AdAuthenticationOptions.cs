namespace AiNexus.Features.Identity.Authentication;

public sealed class AdAuthenticationOptions
{
    public string Mode { get; set; } = "Ldap";
    public string Url { get; set; } = "";
    public string DnUser { get; set; } = "";
    public string DnPass { get; set; } = "";
    public string AdAccountAttrName { get; set; } = "sAMAccountName";
    public string Domain { get; set; } = "";
    public bool Configured => !string.IsNullOrWhiteSpace(DnPass);
}
