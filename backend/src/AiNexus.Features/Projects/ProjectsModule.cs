using AiNexus.Features.AccessControl;
using AiNexus.Features.Projects.Contents;
using AiNexus.Features.Projects.Management;
using AiNexus.Features.Projects.Templates;
using AiNexus.Platform.Dashboard;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Projects;

/// <summary>共用專案：設定、分享、參考檔案、範本，以及使用者在專案中的對話。每個用例各自一個檔案。</summary>
public sealed class ProjectsModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddDashboardSource<ProjectsDashboard>();
        services.AddScoped<ProjectService>();
        services.AddFeaturePolicy(FeatureIds.Projects);
    }

    // 端點順序即已發布的 OpenAPI 順序。
    public static void MapEndpoints(RouteGroupBuilder api)
    {
        AssignConversationProject.Map(api);
        var routes = api.MapGroup("/projects").RequireAuthorization(Policies.Projects).WithTags("Projects");
        BrowseProjects.MapList(routes);
        SaveProject.MapCreate(routes);
        DeleteProject.Map(routes);
        BrowseProjects.MapGet(routes);
        SaveProject.MapUpdate(routes);
        ShareProject.Map(routes);
        ListProjectFiles.Map(routes);
        AddProjectFile.Map(routes);
        ListProjectTemplates.Map(routes);
        SaveProjectTemplate.MapCreate(routes);
        SaveProjectTemplate.MapUpdate(routes);
        DeleteProjectTemplate.Map(routes);
        ListProjectConversations.Map(routes);
        StartProjectConversation.Map(routes);
        CreateProjectArtifact.Map(routes);
    }
}

internal sealed class ProjectsFeatures() : FeatureSeed(new PlatformFeature(FeatureIds.Projects, "專案", "/projects", 15, BuiltInAccess.WorkNavigation, "projects"));
