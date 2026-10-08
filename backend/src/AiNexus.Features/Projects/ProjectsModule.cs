using AiNexus.Features.AccessControl;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Projects;

/// <summary>Shared projects: settings, sharing, reference files, templates and the user's conversations in them. Each use case has its own file.</summary>
public sealed class ProjectsModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddScoped<ProjectService>();
        services.AddScoped<AssignConversationProject>();
        services.AddScoped<SaveProject>();
        services.AddScoped<AddProjectFile>();
        services.AddScoped<SaveProjectTemplate>();
        services.AddScoped<DeleteProjectTemplate>();
        services.AddScoped<StartProjectConversation>();
        services.AddFeaturePolicy(FeatureIds.Projects);
    }

    // Endpoint order is the published OpenAPI order.
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
