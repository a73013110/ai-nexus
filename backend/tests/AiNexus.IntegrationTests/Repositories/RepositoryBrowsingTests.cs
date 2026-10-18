using AiNexus.Data.App;
using AiNexus.Data.App.Attachments;
using AiNexus.Data.App.Knowledge;
using AiNexus.Data.App.Repositories;
using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Jobs;
using AiNexus.Features.Knowledge.Collections;
using AiNexus.Features.Repositories.Browsing;
using AiNexus.Features.Repositories.Handoff;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Repositories.RepositoryConnectionTests;

namespace AiNexus.IntegrationTests.Repositories;

public sealed class RepositoryBrowsingTests
{
    private const string Repo = "/api/v1/repositories/hanglong/nexus";

    [Fact]
    public async Task ReadsArePinnedToOneCommitAndCoverHistoryIssuesAndPulls()
    {
        var source = new FixtureGitea();
        await using var factory = Factory(source); using var client = await factory.SignedInAsync(); await ConnectAsync(client);
        var detail = (await client.GetFromJsonAsync<RepositoryDetailDto>(Repo))!;
        Assert.Equal(FixtureGitea.Head, detail.Commit); Assert.Equal("main", Assert.Single(detail.Branches).Name); Assert.Equal(FixtureGitea.Base, Assert.Single(detail.Tags).Commit);
        Assert.True(detail.Repository.Private); Assert.Equal(1, detail.Repository.OpenPulls);
        var tree = (await client.GetFromJsonAsync<RepositoryTreeDto>($"{Repo}/tree?commit={detail.Commit}"))!;
        Assert.Equal(["docs", "src", "README.md"], tree.Entries.Select(x => x.Name)); Assert.Contains("技術文件", tree.Readme!.Text);
        Assert.All(source.Requests.Where(x => x.Contains("/contents")), x => Assert.Contains("?ref=" + FixtureGitea.Head, x));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"{Repo}/tree?commit=main")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"{Repo}/file?commit={detail.Commit}&path=../secret")).StatusCode);

        var commits = (await client.GetFromJsonAsync<RepositoryCommitPageDto>($"{Repo}/commits?ref=main"))!;
        Assert.Equal("修正登入流程", commits.Items[0].Title); Assert.Equal("fixture-user", commits.Items[0].Author);
        var diff = (await client.GetFromJsonAsync<RepositoryDiffDto>($"{Repo}/diff?head={FixtureGitea.Head}&base={FixtureGitea.Base}"))!;
        Assert.False(diff.TooLarge); Assert.StartsWith("diff --git", diff.Diff); Assert.Equal(FixtureGitea.Base, diff.Base!.Sha);
        Assert.Contains(source.Requests, x => x.EndsWith($"/compare/{FixtureGitea.Base}..{FixtureGitea.Head}?output=diff", StringComparison.Ordinal));
        source.Diff = "diff --git a/a b/a\n" + new string('x', 300000);
        Assert.True((await client.GetFromJsonAsync<RepositoryDiffDto>($"{Repo}/diff?head={FixtureGitea.Head}"))!.TooLarge);

        var issues = (await client.GetFromJsonAsync<RepositoryIssuePageDto>($"{Repo}/issues?state=open"))!;
        Assert.Equal("#ee0701", Assert.Single(Assert.Single(issues.Items).Labels).Color);
        var issue = (await client.GetFromJsonAsync<RepositoryIssueDetailDto>($"{Repo}/issues/1"))!;
        Assert.Equal("請補上測試", Assert.Single(issue.Comments).Body);
        var pull = (await client.GetFromJsonAsync<RepositoryPullDetailDto>($"{Repo}/pulls/2"))!;
        Assert.Equal(FixtureGitea.Head, pull.HeadCommit); Assert.Equal(FixtureGitea.Base, pull.BaseCommit); Assert.Equal("fix/login", pull.Pull.HeadRef);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"{Repo}/issues?state=deleted")).StatusCode);
    }

    [Fact]
    public async Task PinsKeepRepositoriesAtTheTopUntilRemoved()
    {
        await using var factory = Factory(new FixtureGitea()); using var client = await factory.SignedInAsync(); await ConnectAsync(client);
        var pin = await client.PutAsync($"{Repo}/pin", null); pin.EnsureSuccessStatusCode();
        (await client.PutAsync($"{Repo}/pin", null)).EnsureSuccessStatusCode();
        var page = (await client.GetFromJsonAsync<RepositoryPageDto>("/api/v1/repositories"))!;
        Assert.Equal("內部文件", Assert.Single(page.Pinned).Description); Assert.True(Assert.Single(page.Items).Pinned);
        Assert.True((await client.GetFromJsonAsync<RepositoryDetailDto>(Repo))!.Repository.Pinned);
        (await client.DeleteAsync($"{Repo}/pin")).EnsureSuccessStatusCode();
        Assert.Empty((await client.GetFromJsonAsync<RepositoryPageDto>("/api/v1/repositories"))!.Pinned);
    }

    [Fact]
    public async Task ChatHandoffBundlesFreshSourcesIntoOneAttachmentWithoutCallingAModel()
    {
        await using var factory = Factory(new FixtureGitea()); using var client = await factory.SignedInAsync(); await ConnectAsync(client);
        var response = await client.PostAsJsonAsync($"{Repo}/chat", new StartRepositoryChatRequest(FixtureGitea.Head, ["README.md", "docs/guide.md", "docs/logo.png"], null, null));
        response.EnsureSuccessStatusCode(); var chat = (await response.Content.ReadFromJsonAsync<RepositoryChatDto>())!;
        Assert.EndsWith(".md", chat.Attachment.FileName); Assert.Equal(["docs/logo.png"], chat.Skipped); Assert.Contains("勿執行", chat.Prompt);
        using (var scope = factory.Services.CreateScope())
        {
            var text = (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<Attachment>().SingleAsync(x => x.Id == chat.Attachment.Id)).ExtractedText!;
            Assert.Contains("## README.md", text); Assert.Contains("````md\n# 指南", text); Assert.Contains(FixtureGitea.Head, text);
        }
        Assert.Equal(0, factory.Provider.Calls);
        var pull = await client.PostAsJsonAsync($"{Repo}/chat", new StartRepositoryChatRequest(null, null, null, 2)); pull.EnsureSuccessStatusCode();
        Assert.Contains("Pull Request #2", (await pull.Content.ReadFromJsonAsync<RepositoryChatDto>())!.Prompt);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"{Repo}/chat", new StartRepositoryChatRequest(FixtureGitea.Head, ["README.md"], 1, null))).StatusCode);
    }

    [Fact]
    public async Task FolderImportsQueueTextFilesAndReuseEarlierSnapshots()
    {
        await using var factory = Factory(new FixtureGitea()); using var client = await factory.SignedInAsync(); await ConnectAsync(client);
        var create = await client.PostAsJsonAsync("/api/v1/knowledge/collections", new { name = "程式文件", description = "" }); create.EnsureSuccessStatusCode();
        var collection = (await create.Content.ReadFromJsonAsync<CollectionDto>())!.Resource.Id;
        var request = new ImportRepositoryFolderRequest(FixtureGitea.Head, "docs", collection);
        var response = await client.PostAsJsonAsync($"{Repo}/imports", request); response.EnsureSuccessStatusCode();
        var queued = (await response.Content.ReadFromJsonAsync<RepositoryFolderImportDto>())!;
        Assert.Equal(1, queued.Files); Assert.Equal("repository-import", queued.Job.Kind);
        await BackgroundJobs.Drain(factory);
        var job = (await client.GetFromJsonAsync<JobDto>($"/api/v1/jobs/{queued.Job.Id}"))!; Assert.Equal("completed", job.Status);
        var again = (await (await client.PostAsJsonAsync($"{Repo}/imports", request)).Content.ReadFromJsonAsync<RepositoryFolderImportDto>())!;
        await BackgroundJobs.Drain(factory);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.Set<Document>().CountAsync(x => x.CollectionId == collection && !x.IsDeleted));
        Assert.Equal(1, (await db.Set<RepositoryFolderImport>().SingleAsync(x => x.Id == again.Id)).Imported);
        var other = await client.PostAsJsonAsync($"{Repo}/imports", request with { CollectionId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
    }
}
