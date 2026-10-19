using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.AccessControl.Navigation;

namespace AiNexus.UnitTests.AccessControl;

public sealed class NavigationTreeTests
{
    private static readonly NavigationGroupDto[] Groups =
    [
        new("work", null, "AI 工作台", 10),
        new("system", null, "系統管理", 30),
        new("diagnosis", "system", "診斷", 10),
        new("deep", "diagnosis", "深層", 10),
        new("empty", null, "空分群", 40),
    ];

    private static FeatureDto Feature(string id, string? group) => new(id, id, "/x", group, "document", 10);

    [Fact]
    public void VisibleGroupsKeepAncestorsOfCatalogPagesAndDropTheRest()
    {
        // 可見與否看目錄的 Kind，不看 DTO 的 route：動作與目錄不認得的功能只是授權。
        var visible = NavigationTree.VisibleGroups(Groups,
            [Feature(FeatureCatalog.Chat, "work"), Feature(FeatureCatalog.DiagnosticsLogs, "deep"), Feature(FeatureCatalog.DiagnosticsLogsExport, "empty"), Feature("retired", "empty")]);
        Assert.Equal(["work", "system", "diagnosis", "deep"], visible.Select(x => x.Id));
    }

    [Fact]
    public void FeaturesAtTheTopLevelOrInUnknownGroupsNeedNoGroup()
    {
        Assert.Empty(NavigationTree.VisibleGroups(Groups, [Feature(FeatureCatalog.Dashboard, null), Feature(FeatureCatalog.Jobs, "missing")]));
    }

    [Fact]
    public void CyclesAreDetectedIncludingSelfParenting()
    {
        var parents = Groups.ToDictionary(x => x.Id, x => x.ParentId);
        Assert.True(NavigationTree.CreatesCycle(parents, "system", "deep"));
        Assert.True(NavigationTree.CreatesCycle(parents, "system", "system"));
        Assert.False(NavigationTree.CreatesCycle(parents, "deep", "work"));
        Assert.False(NavigationTree.CreatesCycle(parents, "work", null));
    }
}
