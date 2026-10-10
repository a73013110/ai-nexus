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
    public void InvestigationRoutesUseTheirOwnFixedFeature(string route, string expected)
    {
        Assert.Equal(expected, MonitoringVocabulary.Feature(route));
        Assert.True(MonitoringVocabulary.ValidFeature(expected));
    }
}
