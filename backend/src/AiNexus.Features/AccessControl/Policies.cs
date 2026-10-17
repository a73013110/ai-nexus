namespace AiNexus.Features.AccessControl;

/// <summary>平台功能 id，與角色／群組授權表中儲存的值相同。</summary>
public static class FeatureIds
{
    public const string Chat = BuiltInAccess.ChatFeature;
    public const string Projects = "projects";
    public const string Artifacts = "artifacts";
    public const string Shared = "shared";
    public const string Quality = "quality";
    public const string Knowledge = "knowledge";
    public const string Tasks = "tasks";
    public const string Integrations = "integrations";
    public const string Files = "files";
    public const string Dashboard = "dashboard";
    public const string Repositories = "repositories";
    public const string Admin = "admin";
    public const string Operations = "operations";
    public const string Design = "design";
}

/// <summary>
/// 授權 policy 名稱。端點一律引用這些常數而非字串字面值；<c>EndpointConventionTests</c> 會檢查每個被引用的 policy 都已註冊。
/// </summary>
public static class Policies
{
    public const string Prefix = "feature:";
    public const string Chat = BuiltInAccess.ChatPolicy;
    public const string Projects = Prefix + FeatureIds.Projects;
    public const string Artifacts = Prefix + FeatureIds.Artifacts;
    public const string Shared = Prefix + FeatureIds.Shared;
    public const string Quality = Prefix + FeatureIds.Quality;
    public const string Knowledge = Prefix + FeatureIds.Knowledge;
    public const string Tasks = Prefix + FeatureIds.Tasks;
    public const string Integrations = Prefix + FeatureIds.Integrations;
    public const string Files = Prefix + FeatureIds.Files;
    public const string Dashboard = Prefix + FeatureIds.Dashboard;
    public const string Repositories = Prefix + FeatureIds.Repositories;
    public const string Admin = Prefix + FeatureIds.Admin;
    public const string Operations = Prefix + FeatureIds.Operations;

    /// <summary>對話或成果：兩個入口都可使用的文字工具。</summary>
    public const string Text = Prefix + "text";

    /// <summary>任何可以持有附件的功能：檔案、對話、知識庫或專案。</summary>
    public const string Attachments = Prefix + "attachments";
}
