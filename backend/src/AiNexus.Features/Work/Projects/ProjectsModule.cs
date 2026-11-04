using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Work.Projects.Contents;
using AiNexus.Features.Work.Projects.Management;
using AiNexus.Features.Work.Projects.Templates;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Work.Projects;

/// <summary>共用專案：設定、分享、參考檔案、範本，以及使用者在專案中的對話。每個用例各自一個檔案。</summary>
public sealed class ProjectsModule : IFeatureModule
{
    // 端點順序即已發布的 OpenAPI 順序。
    public static void MapEndpoints(RouteGroupBuilder api)
    {
        api.MapSlice<AssignConversationProject>();
        api.MapGroup("/projects").RequireFeature(FeatureCatalog.Projects)
            .MapSlice<BrowseProjects>()
            .MapSlice<SaveProject>()
            .MapSlice<DeleteProject>()
            .MapSlice<ShareProject>()
            .MapSlice<ListProjectFiles>()
            .MapSlice<AddProjectFile>()
            .MapSlice<ListProjectTemplates>()
            .MapSlice<SaveProjectTemplate>()
            .MapSlice<DeleteProjectTemplate>()
            .MapSlice<ListProjectConversations>()
            .MapSlice<StartProjectConversation>()
            .MapSlice<CreateProjectArtifact>();
    }
}
