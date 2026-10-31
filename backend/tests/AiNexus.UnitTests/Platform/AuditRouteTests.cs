using AiNexus.Platform.Auditing;
using Microsoft.AspNetCore.Routing.Patterns;

namespace AiNexus.UnitTests.Platform;

public sealed class AuditRouteTests
{
    [Theory]
    [InlineData("/api/v1/artifacts/{id:guid}", "id", null)]
    [InlineData("/api/v1/admin/users/{id:guid}/access", "id", "id")]
    [InlineData("/api/v1/admin/users/{userId:guid}/sessions/{id:guid}", "id", "userId")]
    [InlineData("/api/v1/admin/storage/{userId:guid}", null, "userId")]
    [InlineData("/api/v1/users-archive/{id:guid}", "id", null)]
    [InlineData("/api/v1/artifacts", null, null)]
    public void RouteParametersBecomeResourceAndSubject(string pattern, string? resource, string? subject)
        => Assert.Equal(new AuditRoute(resource, subject), AuditRoute.Of(RoutePatternFactory.Parse(pattern)));
}
