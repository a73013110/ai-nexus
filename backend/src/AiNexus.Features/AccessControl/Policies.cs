namespace AiNexus.Features.AccessControl;

/// <summary>Platform feature identifiers as stored in role/group grants.</summary>
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
}

/// <summary>
/// Authorization policy names. Endpoints reference these constants, never string literals, and
/// <c>EndpointConventionTests</c> checks that every referenced policy is registered.
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

    /// <summary>Chat or artifacts: text tools available from either surface.</summary>
    public const string Text = Prefix + "text";

    /// <summary>Any feature that can hold attachments: files, chat, knowledge or projects.</summary>
    public const string Attachments = Prefix + "attachments";
}
