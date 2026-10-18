using System.Reflection;

namespace AiNexus.Features.AccessControl;

/// <summary>
/// 平台功能 id 的唯一來源，與 <c>accesscontrol.Features</c> 的資料列相同；模組自己的功能常數也引用這裡。
/// 新增或改名功能時同時改 <c>database/App/Seed/accesscontrol.sql</c>，<c>BuiltInAccessSeedTests</c> 會雙向比對；
/// OpenAPI 以 <see cref="All"/> 輸出 <c>FeatureId</c> 列舉，前端的功能 id 型別由此產生。
/// </summary>
public static class FeatureIds
{
    public const string Chat = "chat";
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
    public const string Monitoring = "monitoring";
    public const string Audit = "audit";
    public const string LogsQuery = "logs.query";
    /// <summary>只是授權、不出現在選單（Seed 的 route 為空）。</summary>
    public const string LogsDetail = "logs.detail", LogsExport = "logs.export";
    public const string Design = "design";

    /// <summary>上面所有常數，依序數排序。</summary>
    public static IReadOnlyList<string> All { get; } = [.. typeof(FeatureIds).GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.IsLiteral).Select(field => (string)field.GetRawConstantValue()!).Order(StringComparer.Ordinal)];
}

/// <summary>
/// 授權 policy 名稱。端點一律引用這些常數而非字串字面值；<c>EndpointConventionTests</c> 會檢查每個被引用的 policy 都已註冊。
/// </summary>
public static class Policies
{
    public const string Prefix = "feature:";
    public const string Chat = Prefix + FeatureIds.Chat;
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
