using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Account;
using AiNexus.Features.Administration.Users;
using AiNexus.Features.Artifacts;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Conversations;
using AiNexus.Features.Knowledge.Documents;
using AiNexus.Features.Persistence;
using AiNexus.Features.Projects;
using AiNexus.Platform.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.ChatApi;

namespace AiNexus.IntegrationTests.Persistence;

public sealed class SoftDeleteFilterTests
{
    [Fact]
    public void OnlySoftDeletedEntitiesDeclareTheNamedFilterAndTheModelBuildsWithoutFilterWarnings()
    {
        using var connection = new SqliteConnection("Data Source=:memory:"); connection.Open();
        // A separate options set builds and validates its own model, so the required-navigation warning would throw here.
        using var db = new NexusDbContext(new DbContextOptionsBuilder<NexusDbContext>().UseSqlite(connection)
            .ConfigureWarnings(w => w.Throw(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning)).Options);
        var filtered = db.Model.GetEntityTypes().Where(x => x.GetDeclaredQueryFilters().Count > 0).Select(x => x.ClrType).OrderBy(x => x.Name).ToArray();
        Assert.Equal([typeof(Conversation), typeof(WorkspaceResource)], filtered);
        Assert.All(filtered, type => Assert.Equal(SoftDelete.Filter, Assert.Single(db.Model.FindEntityType(type)!.GetDeclaredQueryFilters()).Key));
        db.Database.EnsureCreated();
    }

    [Fact]
    public async Task DeletedConversationIsHiddenByDefaultButStillVisibleToAdministrativeReads()
    {
        await using var factory = new NexusFactory(administrators: ["alice"]);
        using var admin = await factory.SignedInAsync(); using var bob = await factory.SignedInAsync("bob");
        var user = (await bob.GetFromJsonAsync<MeDto>("/api/v1/me"))!;
        var kept = await CreateConversation(bob, "kept"); var deleted = await CreateConversation(bob, "deleted");
        Assert.Equal(HttpStatusCode.NoContent, (await bob.DeleteAsync($"/api/v1/conversations/{deleted.Id}")).StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
            Assert.Equal([kept.Id], await db.Conversations.Where(x => x.OwnerId == user.Id).Select(x => x.Id).ToListAsync());
            Assert.Null(await db.Conversations.FirstOrDefaultAsync(x => x.Id == deleted.Id));
            var row = await db.Conversations.IgnoreQueryFilters([SoftDelete.Filter]).SingleAsync(x => x.Id == deleted.Id);
            Assert.True(row.IsDeleted);
            // Joins filter the joined conversation too: messages of the deleted conversation drop out.
            var joined = await (from m in db.Messages join c in db.Conversations on m.ConversationId equals c.Id where c.OwnerId == user.Id select c.Id).Distinct().ToListAsync();
            Assert.DoesNotContain(deleted.Id, joined);
        }

        var listed = (await bob.GetFromJsonAsync<ConversationDto[]>("/api/v1/conversations"))!;
        Assert.Contains(listed, x => x.Id == kept.Id); Assert.DoesNotContain(listed, x => x.Id == deleted.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/v1/conversations/{deleted.Id}")).StatusCode);

        var page = (await admin.GetFromJsonAsync<AdminConversationPageDto>($"/api/v1/admin/users/{user.Id}/conversations?includeDeleted=true"))!;
        Assert.True(Assert.Single(page.Items, x => x.Id == deleted.Id).IsDeleted);
        Assert.DoesNotContain((await admin.GetFromJsonAsync<AdminConversationPageDto>($"/api/v1/admin/users/{user.Id}/conversations"))!.Items, x => x.Id == deleted.Id);
        Assert.True((await admin.GetFromJsonAsync<AdminConversationDetailDto>($"/api/v1/admin/conversations/{deleted.Id}"))!.Conversation.IsDeleted);
        Assert.Equal(2, (await admin.GetFromJsonAsync<AdminUserDetailDto>($"/api/v1/admin/users/{user.Id}/insights"))!.Conversations);
    }

    [Fact]
    public async Task DeletedResourceIsHiddenByDefaultAndProjectDeletionStillDetachesDeletedRows()
    {
        await using var factory = new NexusFactory();
        using var owner = await factory.SignedInAsync();
        var me = (await owner.GetFromJsonAsync<MeDto>("/api/v1/me"))!;
        var artifact = (await (await owner.PostAsJsonAsync("/api/v1/artifacts", new CreateArtifactRequest("gone", "body"))).Content.ReadFromJsonAsync<ArtifactDto>())!;
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/v1/artifacts/{artifact.Resource.Id}")).StatusCode);
        var project = (await (await owner.PostAsJsonAsync("/api/v1/projects", new ProjectRequest("container"))).Content.ReadFromJsonAsync<ProjectDto>())!.Resource.Id;
        var deletedChild = new WorkspaceResource { OwnerId = me.Id, Kind = KnowledgeDocument.Kind, Name = "deleted file", ParentId = project, IsDeleted = true };
        var deletedConversation = new Conversation { OwnerId = me.Id, ProjectId = project, Title = "deleted chat", IsDeleted = true };
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
            db.Add(deletedChild); db.Add(deletedConversation); await db.SaveChangesAsync();
            Assert.False(await db.Set<WorkspaceResource>().AnyAsync(x => x.Id == artifact.Resource.Id));
            Assert.Equal([project], await db.Set<WorkspaceResource>().Where(x => x.OwnerId == me.Id).Select(x => x.Id).ToListAsync());
            Assert.True((await db.Set<WorkspaceResource>().IgnoreQueryFilters([SoftDelete.Filter]).SingleAsync(x => x.Id == artifact.Resource.Id)).IsDeleted);
        }
        Assert.Empty((await owner.GetFromJsonAsync<ArtifactSummaryDto[]>("/api/v1/artifacts"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/v1/artifacts/{artifact.Resource.Id}")).StatusCode);
        Assert.Empty((await owner.GetFromJsonAsync<DocumentDto[]>($"/api/v1/projects/{project}/files"))!);

        // Container deletion opts out of the filter: deleted children and conversations are detached as before.
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/v1/projects/{project}")).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
            Assert.Null((await db.Set<WorkspaceResource>().IgnoreQueryFilters([SoftDelete.Filter]).SingleAsync(x => x.Id == deletedChild.Id)).ParentId);
            Assert.Null((await db.Conversations.IgnoreQueryFilters([SoftDelete.Filter]).SingleAsync(x => x.Id == deletedConversation.Id)).ProjectId);
            Assert.Empty(await db.Set<WorkspaceResource>().Where(x => x.OwnerId == me.Id).ToListAsync());
        }
    }
}
