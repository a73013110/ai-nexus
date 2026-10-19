using AiNexus.Features.Admin.Monitoring;

namespace AiNexus.UnitTests.Monitoring;

public sealed class MonitoringVocabularyTests
{
    [Theory]
    [InlineData("/api/v1/admin/audit", "audit")]
    [InlineData("/api/v1/admin/audit/catalog", "audit")]
    [InlineData("/api/v1/admin/logs/{id:guid}", "diagnostics.logs")]
    [InlineData("/api/v1/admin/monitoring/snapshot", "monitoring")]
    [InlineData("/api/v1/admin/audit-other", "identity.users")]
    [InlineData("/api/v1/admin/users/{id:guid}", "identity.users")]
    [InlineData("/api/v1/admin/catalog", "identity.users")]
    [InlineData("/api/v1/admin/roles/{id}", "access-control.roles")]
    [InlineData("/api/v1/admin/groups/{id}", "access-control.groups")]
    [InlineData("/api/v1/admin/features/{id}", "access-control.features")]
    [InlineData("/api/v1/admin/navigation-items/{id}", "access-control.navigation")]
    [InlineData("/api/v1/admin/navigation-items/order", "access-control.navigation")]
    [InlineData("/api/v1/admin/usage", "operations.usage")]
    [InlineData("/api/v1/admin/knowledge/profiles/{id:int}/rebuild", "knowledge.retrieval")]
    [InlineData("/api/v1/admin/billing/prices", "billing.prices")]
    [InlineData("/api/v1/admin/billing/spend", "operations.usage")]
    public void AdministrativeRoutesUseThePageThatCallsThem(string route, string expected)
    {
        Assert.Equal(expected, MonitoringVocabulary.Feature(route));
        Assert.True(MonitoringVocabulary.ValidFeature(expected));
    }

    [Theory]
    [InlineData("/api/v1/conversations/{id:guid}", "chat")]
    [InlineData("/api/v1/jobs", "jobs")]
    [InlineData("/api/v1/shares", "sharing")]
    [InlineData("/api/v1/attachments/{id:guid}", "files")]
    [InlineData("/api/v1/documents/{id:guid}", "knowledge")]
    [InlineData("/api/v1/projects", "projects")]
    [InlineData("/api/v1/quality/sets", "quality")]
    [InlineData("/api/v1/quality/retrieval-evals", "quality.retrieval")]
    [InlineData("/api/v1/me", "settings")]
    [InlineData("/api/v1/status", "system")]
    public void ResourceRoutesUseTheirPage(string route, string expected) => Assert.Equal(expected, MonitoringVocabulary.Feature(route));

    [Theory]
    [InlineData("identity.users", "使用者")]
    [InlineData("operations.usage", "平台用量")]
    [InlineData("knowledge.reader", "文件閱讀器")]
    [InlineData("settings", "個人設定")]
    public void PresenceAcceptsCatalogPagesReadersAndSettingsWithCatalogNames(string feature, string name)
    {
        Assert.True(MonitoringVocabulary.ValidFeature(feature));
        Assert.Equal(name, MonitoringVocabulary.Name(feature));
    }

    [Theory]
    [InlineData("diagnostics.logs.export")]
    [InlineData("admin")]
    [InlineData("/work/chat")]
    [InlineData("system")]
    public void PresenceRejectsActionsOldIdsAndPaths(string feature) => Assert.False(MonitoringVocabulary.ValidFeature(feature));
}
