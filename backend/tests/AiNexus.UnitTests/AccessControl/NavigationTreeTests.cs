using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.AccessControl.Navigation;
using AiNexus.Platform.Configuration;

namespace AiNexus.UnitTests.AccessControl;

public sealed class NavigationTreeTests
{
    private static readonly string[] Hosts = ["intranet.example.com", "*.docs.example.com", "tools.example.com:8443"];

    private static NavigationItemDto Group(string id, string? parent, int order, string name = "分群") => new(id, parent, "group", null, name, null, true, "apps", order, []);
    private static NavigationItemDto Page(string id, string? parent, string feature, int order) => new(id, parent, "page", feature, null, null, true, null, order, []);
    private static NavigationItemDto Link(string id, string? parent, string url, int order, params string[] audiences) => new(id, parent, "link", null, "連結", url, false, null, order, audiences);
    private static FeatureDto Feature(string id, string? name = null) => new(id, name ?? FeatureCatalog.Find(id)?.Name ?? id, FeatureCatalog.Find(id)?.Route ?? "", "x");

    private static IReadOnlyList<NavigationNodeDto> Build(IReadOnlyList<NavigationItemDto> items, IEnumerable<FeatureDto> features, params string[] groups)
        => NavigationTree.Build(items, features, groups, Hosts);

    [Fact]
    public void PagesShowOnlyGrantedCatalogPagesWithCatalogRouteIconAndEffectiveName()
    {
        NavigationItemDto[] items =
        [
            Page("dashboard", null, FeatureCatalog.Dashboard, 10),
            Page("chat", null, FeatureCatalog.Chat, 20),
            // 動作只是授權、目錄不認得的功能沒有網址：都不會出現在選單。
            Page("export", null, FeatureCatalog.DiagnosticsLogsExport, 30),
            Page("retired", null, "retired", 40),
            Page("jobs", null, FeatureCatalog.Jobs, 50),
        ];
        var tree = Build(items, [Feature(FeatureCatalog.Chat, "聊天"), Feature(FeatureCatalog.Dashboard), Feature(FeatureCatalog.DiagnosticsLogsExport), Feature("retired")]);
        Assert.Equal(["dashboard", "chat"], tree.Select(x => x.Id));
        var chat = tree[1];
        Assert.Equal(("page", "聊天", "chat", FeatureCatalog.Chat, "/work/chat", (string?)null, false), (chat.Kind, chat.Name, chat.Icon, chat.FeatureId, chat.Route, chat.Url, chat.OpenInNewTab));
        Assert.Empty(chat.Children);
    }

    [Fact]
    public void EmptyGroupsArePrunedLevelByLevelAndSingleChildGroupsStay()
    {
        NavigationItemDto[] items =
        [
            Group("work", null, 20, "工作"),
            Page("work.chat", "work", FeatureCatalog.Chat, 10),
            Page("work.projects", "work", FeatureCatalog.Projects, 20),
            Group("system", null, 30),
            Group("system.access", "system", 10),
            Group("system.access.deep", "system.access", 10),
            Page("system.access.deep.users", "system.access.deep", FeatureCatalog.IdentityUsers, 10),
            Group("empty", null, 40),
            Group("hidden", null, 50),
            Page("hidden.audit", "hidden", FeatureCatalog.Audit, 10),
            // 子分群全被修剪：上層也沒有可見項目。
            Group("nested", null, 60),
            Group("nested.inner", "nested", 10),
            Page("nested.inner.audit", "nested.inner", FeatureCatalog.Audit, 10),
        ];
        var tree = Build(items, [Feature(FeatureCatalog.Chat), Feature(FeatureCatalog.Projects), Feature(FeatureCatalog.IdentityUsers)]);
        Assert.Equal(["work", "system"], tree.Select(x => x.Id));
        Assert.Equal(("group", "工作", "apps"), (tree[0].Kind, tree[0].Name, tree[0].Icon));
        Assert.Equal(["work.chat", "work.projects"], tree[0].Children.Select(x => x.Id));
        // 只剩一項的分群仍保留，一路到葉節點。
        var access = Assert.Single(tree[1].Children);
        Assert.Equal(("system.access", "group"), (access.Id, access.Kind));
        var deep = Assert.Single(access.Children);
        Assert.Equal(("system.access.deep", "group"), (deep.Id, deep.Kind));
        Assert.Equal(("system.access.deep.users", "page"), (Assert.Single(deep.Children).Id, Assert.Single(deep.Children).Kind));
    }

    [Fact]
    public void SiblingsAreOrderedBySortOrderThenOrdinalId()
    {
        NavigationItemDto[] items = [Page("b", null, FeatureCatalog.Projects, 10), Page("a", null, FeatureCatalog.Files, 10), Page("c", null, FeatureCatalog.Chat, 5)];
        Assert.Equal(["c", "a", "b"], Build(items, [Feature(FeatureCatalog.Chat), Feature(FeatureCatalog.Files), Feature(FeatureCatalog.Projects)]).Select(x => x.Id));
    }

    [Fact]
    public void LinksNeedAnAudienceMatchAndAnAllowedHost()
    {
        NavigationItemDto[] items =
        [
            Link("everyone", null, "https://intranet.example.com/news?x=1", 10),
            Link("staff", null, "https://intranet.example.com/hr", 20, "staff", "managers"),
            Link("managers", null, "https://intranet.example.com/budget", 30, "managers"),
            Link("subdomain", null, "https://wiki.docs.example.com/", 40),
            Link("apex", null, "https://docs.example.com/", 50),
            Link("port", null, "https://tools.example.com:8443/x", 60),
            Link("wrong-port", null, "https://intranet.example.com:8443/", 70),
            Link("removed", null, "https://old.example.com/", 80),
        ];
        var tree = Build(items, [], "staff");
        Assert.Equal(["everyone", "staff", "subdomain", "port"], tree.Select(x => x.Id));
        var link = tree[0];
        Assert.Equal(("link", "連結", "https://intranet.example.com/news?x=1", (string?)null, (string?)null, false), (link.Kind, link.Name, link.Url, link.Route, link.FeatureId, link.OpenInNewTab));
        Assert.Empty(NavigationTree.Build(items, [], ["staff"], []));
    }

    [Fact]
    public void AGroupWhoseOnlyVisibleChildIsALinkKeepsTheGroup()
    {
        NavigationItemDto[] items = [Group("tools", null, 10), Link("tools.wiki", "tools", "https://intranet.example.com/wiki", 10), Link("tools.secret", "tools", "https://intranet.example.com/s", 20, "admins")];
        var tools = Assert.Single(Build(items, []));
        Assert.Equal(("tools", "group"), (tools.Id, tools.Kind));
        Assert.Equal("tools.wiki", Assert.Single(tools.Children).Id);
    }

    [Fact]
    public void CyclicDataDoesNotRecurseForever()
    {
        NavigationItemDto[] items = [Group("a", "b", 10), Group("b", "a", 10), Page("a.chat", "a", FeatureCatalog.Chat, 10)];
        Assert.Empty(Build(items, [Feature(FeatureCatalog.Chat)]));
    }

    [Fact]
    public void CyclesAreDetectedIncludingSelfParenting()
    {
        Assert.True(NavigationTree.HasCycle(new Dictionary<string, string?> { ["a"] = "b", ["b"] = "a" }));
        Assert.True(NavigationTree.HasCycle(new Dictionary<string, string?> { ["a"] = "a" }));
        Assert.False(NavigationTree.HasCycle(new Dictionary<string, string?> { ["a"] = null, ["b"] = "a", ["c"] = "b", ["d"] = "missing" }));
    }

    [Fact]
    public void DeepestCountsTheTopLevelAsOne()
    {
        Assert.Equal(0, NavigationTree.Deepest(new Dictionary<string, string?>()));
        Assert.Equal(4, NavigationTree.Deepest(new Dictionary<string, string?> { ["a"] = null, ["b"] = "a", ["c"] = "b", ["d"] = "c", ["e"] = null }));
    }

    [Fact]
    public void SectionsPlaceEachFeatureAtItsFirstPlaceInTheFullTreeWithActionsAfterTheirPage()
    {
        NavigationItemDto[] items =
        [
            Page("dashboard", null, FeatureCatalog.Dashboard, 10),
            Group("work", null, 20, "工作"),
            Page("work.chat", "work", FeatureCatalog.Chat, 10),
            Group("system", null, 30, "系統管理"),
            // 子分群排在同層頁面之前，區段順序跟著側欄由上而下。
            Group("system.access", "system", 10, "身分與權限"),
            Page("system.access.users", "system.access", FeatureCatalog.IdentityUsers, 10),
            Page("system.logs", "system", FeatureCatalog.DiagnosticsLogs, 20),
            Page("system.retrieval", "system", FeatureCatalog.KnowledgeRetrieval, 30),
            // 只有一個子節點的分群不攤平；第二次出現的頁面、動作與目錄不認得的 ID 都不另佔位置。
            Group("more", null, 40, "更多"),
            Page("more.chat", "more", FeatureCatalog.Chat, 10),
            Page("more.export", "more", FeatureCatalog.DiagnosticsLogsExport, 20),
            Page("more.retired", "more", "retired", 30),
            Link("more.wiki", "more", "https://intranet.example.com/", 40),
            Group("empty", null, 50),
        ];
        var sections = NavigationTree.Sections(items);
        Assert.Equal(["", "work", "system.access", "system"], sections.Select(x => x.Id));
        Assert.Equal(["系統管理", "身分與權限"], sections[2].Path);
        Assert.Empty(sections[0].Path);
        Assert.Equal([FeatureCatalog.DiagnosticsLogs, FeatureCatalog.DiagnosticsLogsDetail, FeatureCatalog.DiagnosticsLogsExport, FeatureCatalog.KnowledgeRetrieval],
            sections[3].FeatureIds);
    }

    [Theory]
    [InlineData("https://intranet.example.com/path?q=1#top", true)]
    [InlineData("HTTPS://intranet.example.com/", true)]
    [InlineData("http://intranet.example.com/", false)]
    [InlineData("https://user:secret@intranet.example.com/", false)]
    [InlineData("https:/intranet.example.com/", false)]
    [InlineData("//intranet.example.com/", false)]
    [InlineData("/work/chat", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("https://intranet.example.com/a b", false)]
    [InlineData("https://[::1]/", false)]
    [InlineData("", false)]
    public void LinksMustBeAbsoluteHttpsWithoutUserInfo(string url, bool expected) => Assert.Equal(expected, NavigationLinks.WellFormed(url));

    [Theory]
    [InlineData("https://INTRANET.example.com/", true)]
    [InlineData("https://intranet.example.com:443/", true)]
    [InlineData("https://evil-intranet.example.com/", false)]
    [InlineData("https://intranet.example.com.evil.test/", false)]
    [InlineData("https://a.b.docs.example.com/", true)]
    [InlineData("https://xdocs.example.com/", false)]
    [InlineData("https://tools.example.com/", false)]
    public void HostsMatchExactlyOrAsSubdomainsWithTheirPort(string url, bool expected) => Assert.Equal(expected, NavigationLinks.HostAllowed(url, Hosts));

    [Theory]
    [InlineData("intranet.example.com", true)]
    [InlineData("*.example.com", true)]
    [InlineData("tools.example.com:8443", true)]
    [InlineData("10.0.0.5", true)]
    [InlineData("Intranet.example.com", false)]
    [InlineData("https://intranet.example.com", false)]
    [InlineData("intranet.example.com/path", false)]
    [InlineData("*.", false)]
    [InlineData("a.*.example.com", false)]
    [InlineData("example.com:0", false)]
    [InlineData("example.com:99999", false)]
    [InlineData("-bad.example.com", false)]
    [InlineData("", false)]
    public void AllowListItemsAreLowercaseHostPatterns(string pattern, bool expected) => Assert.Equal(expected, HostPatternItemsAttribute.Valid(pattern));
}
