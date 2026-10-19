using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Work.Projects.Contents;
using AiNexus.Features.Work.Projects.Management;
using AiNexus.Features.Work.Projects.Templates;
using AiNexus.Platform.Dashboard;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Work.Projects;

/// <summary>共用專案：設定、分享、參考檔案、範本，以及使用者在專案中的對話。每個用例各自一個檔案。</summary>
public sealed class ProjectsModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddDashboardSource<ProjectsDashboard>();
        services.AddScoped<ProjectService>();
        services.AddFeaturePolicy(FeatureCatalog.Projects);
    }

    // 端點順序即已發布的 OpenAPI 順序。
    public static void MapEndpoints(RouteGroupBuilder api)
    {
        AssignConversationProject.Map(api);
        var routes = api.MapGroup("/projects").RequireAuthorization(Policies.Projects);
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
