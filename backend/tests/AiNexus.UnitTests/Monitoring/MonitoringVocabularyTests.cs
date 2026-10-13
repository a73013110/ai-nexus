using AiNexus.Features.Monitoring;

namespace AiNexus.UnitTests.Monitoring;

public sealed class MonitoringVocabularyTests
{
    [Theory]
    [InlineData("/api/v1/admin/audit", "audit")]
    [InlineData("/api/v1/admin/audit/catalog", "audit")]
    [InlineData("/api/v1/admin/logs/{id:guid}", "logs")]
    [InlineData("/api/v1/admin/monitoring/snapshot", "monitoring")]
    [InlineData("/api/v1/admin/audit-other", "admin")]
    [InlineData("/api/v1/admin/users/{id:guid}", "admin")]
    [InlineData("/api/v1/admin/usage", "operations")]
    [InlineData("/api/v1/admin/knowledge/profiles/{id:int}/rebuild", "operations")]
    [InlineData("/api/v1/admin/billing/prices", "operations")]
    public void InvestigationRoutesUseTheirOwnFixedFeature(string route, string expected)
    {
        Assert.Equal(expected, MonitoringVocabulary.Feature(route));
        Assert.True(MonitoringVocabulary.ValidFeature(expected));
    }

    [Theory]
    [InlineData("admin", "存取管理")]
    [InlineData("operations", "平台營運")]
    public void ManagementFeaturesHaveTheirOwnNames(string feature, string name)
    {
        Assert.True(MonitoringVocabulary.ValidFeature(feature));
        Assert.Equal(name, MonitoringVocabulary.Name(feature));
    }
}
