using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using AiNexus.Data.App;
using AiNexus.Data.App.Audit;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Audit;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Platform.Account;
using AiNexus.Platform.Auditing;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AiNexus.IntegrationTests.Audit;

/// <summary>請求稽核（ADR 0023）：端點宣告、動作宣告，以及成功、失敗、拒絕與重複查詢各寫出什麼。</summary>
public sealed class RequestAuditTests
{
    [Fact]
    public async Task EveryApiEndpointDeclaresAnAuditActionOrAStatedReason()
    {
        await using var factory = new AppFactory();
        _ = factory.CreateClient();
        var missing = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.StartsWith("/api/v1/", StringComparison.Ordinal) == true)
            .Where(e => e.Metadata.GetMetadata<AuditedEndpoint>() is null && e.Metadata.GetMetadata<UnauditedEndpoint>() is null)
            .Select(e => $"{string.Join(',', e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])} {e.RoutePattern.RawText}")
            .Order(StringComparer.Ordinal).ToList();
        Assert.True(missing.Count == 0, "API endpoints need .Audited(action) or .NotAudited(reason):\n" + string.Join('\n', missing));
    }

    // 宣告了稽核卻不在掛請求稽核的群組底下，動作只是標註，什麼都不會寫。
    [Fact]
    public async Task EveryAuditedEndpointRunsTheRequestAuditFilter()
    {
        await using var factory = new AppFactory();
        _ = factory.CreateClient();
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToList();
        var unfiltered = endpoints.Where(e => e.AuditActionOf() is not null && e.Metadata.GetMetadata<RequestAudit.Applied>() is null)
            .Select(e => e.RoutePattern.RawText).Order(StringComparer.Ordinal).ToList();
        Assert.Contains(endpoints, e => e.AuditActionOf() == IdentityAudit.TestIdentityEnded);
        Assert.True(unfiltered.Count == 0, "Audited endpoints outside a WithAudit() group:\n" + string.Join('\n', unfiltered));
    }

    // 同一個名稱在不同類別宣告成不同分類或標籤，稽核頁會只顯示其中一個。
    [Fact]
    public void EveryActionNameHasOneDefinitionAndAKnownModule()
    {
        var declared = typeof(AuditActions).Assembly.GetTypes()
            .Where(x => x is { IsAbstract: true, IsSealed: true })
            .SelectMany(x => x.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(x => x.FieldType == typeof(AuditAction))
            .Select(x => (AuditAction)x.GetValue(null)!).ToList();
        Assert.NotEmpty(declared);
        var conflicting = declared.GroupBy(x => x.Name).Where(g => g.Distinct().Count() > 1).Select(g => g.Key).ToList();
        Assert.True(conflicting.Count == 0, "Audit actions declared with different category or label: " + string.Join(", ", conflicting));
        var unknown = AuditActions.Modules.Where(x => x.Label == x.Value).Select(x => x.Value).ToList();
        Assert.True(unknown.Count == 0, "Audit action modules missing from the feature catalog: " + string.Join(", ", unknown));
    }

    [Fact]
    public async Task ReadRecordsFeatureSubjectAndConditionsAndRepeatsWithinTheWindowAreMerged()
    {
        await using var factory = new AppFactory(administrators: ["alice"]);
        using var alice = await factory.SignedInAsync(); using var bob = await factory.SignedInAsync("bob");
        var aliceId = (await alice.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id; var bobId = (await bob.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;

        for (var i = 0; i < 3; i++) (await alice.GetAsync($"/api/v1/admin/users/{bobId}/access")).EnsureSuccessStatusCode();
        (await alice.GetAsync($"/api/v1/admin/users/{aliceId}/access")).EnsureSuccessStatusCode();

        var rows = await RowsAsync(factory, IdentityAudit.UserAccessRead);
        Assert.Equal([bobId, aliceId], rows.Select(x => x.SubjectUserId));
        var read = rows[0];
        Assert.Equal((aliceId, AuditEvent.Completed, "access", FeatureCatalog.IdentityUsers, (Guid?)bobId),
            (read.OwnerId, read.Result, read.Category, read.FeatureId, read.ResourceId));
        Assert.Contains(bobId.ToString(), read.DetailsJson);
    }

    [Fact]
    public async Task FailedRequestRecordsTheErrorCode()
    {
        await using var factory = new AppFactory(administrators: ["alice"]);
        using var alice = await factory.SignedInAsync();
        var missing = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.NotFound, (await alice.GetAsync($"/api/v1/admin/users/{missing}/access")).StatusCode);
        // 失敗不合併：每次嘗試都要留下。
        Assert.Equal(HttpStatusCode.NotFound, (await alice.GetAsync($"/api/v1/admin/users/{missing}/access")).StatusCode);

        var rows = await RowsAsync(factory, IdentityAudit.UserAccessRead);
        Assert.Equal(2, rows.Count);
        Assert.All(rows, row => Assert.Equal(("admin_resource_not_found", (Guid?)missing), (row.Result, row.SubjectUserId)));
    }

    [Fact]
    public async Task FeatureDenialIsRecordedOnceWithTheRequiredFeature()
    {
        await using var factory = new AppFactory(administrators: ["alice"]);
        using var bob = await factory.SignedInAsync("bob");
        var bobId = (await bob.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;

        Assert.Equal(HttpStatusCode.Forbidden, (await bob.GetAsync($"/api/v1/admin/users/{bobId}/access")).StatusCode);

        var denied = Assert.Single(await RowsAsync(factory, IdentityAudit.UserAccessRead));
        Assert.Equal((bobId, AccessControlErrors.FeatureForbidden.Code, FeatureCatalog.IdentityUsers), (denied.OwnerId, denied.Result, denied.FeatureId));
    }

    private static async Task<List<AuditEvent>> RowsAsync(AppFactory factory, AuditAction action)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.AuditEvents.AsNoTracking().Where(x => x.Action == action.Name).OrderBy(x => x.Id).ToListAsync();
    }
}
