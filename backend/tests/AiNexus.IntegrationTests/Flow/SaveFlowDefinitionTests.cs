using System.Net;
using System.Net.Http.Json;
using AiNexus.Data.App.Flow;
using AiNexus.Features.Office.Flow.Definitions;
using static AiNexus.IntegrationTests.Support.FlowApi;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;

namespace AiNexus.IntegrationTests.Flow;

public sealed class SaveFlowDefinitionTests
{
    private const string Kind = "repository-access";

    private static async Task<FlowDefinitionDto> DefinitionAsync(HttpClient admin)
        => (await admin.GetFromJsonAsync<FlowDefinitionsDto>(Definitions))!.Kinds.Single(x => x.Kind == Kind);

    private static FlowStepRequest Step(string name, string rule, string? argument = null, string? fact = null, string? value = null) => new(name, rule, argument, fact, value);

    private static Task<HttpResponseMessage> SaveAsync(HttpClient admin, long? version, params FlowStepRequest[] steps)
        => admin.PutAsJsonAsync($"{Definitions}/{Kind}", new FlowDefinitionRequest(steps, version));

    [Fact]
    public async Task ConditionSkipsTheManagerForReadRequestsOnlyInRequestsSubmittedAfterTheChange()
    {
        await using var session = await RepositoryAccessSession.StartAsync();
        using var alice = await session.ClientAsync("alice"); using var bob = await session.ClientAsync("bob"); using var carol = await session.ClientAsync("carol");
        var before = await SubmitAsync(session, bob);
        var definition = await DefinitionAsync(alice);
        Assert.Equal(["applicant-manager", "repository-stewards"], definition.Steps.Select(x => x.ApproverRule));

        var saved = await SaveAsync(alice, definition.RowVersion,
            Step("主管", "applicant-manager", """{"levels":1}""", "permission", "write"), Step("儲存庫負責人", "repository-stewards"));
        Assert.True(saved.IsSuccessStatusCode, await saved.Content.ReadAsStringAsync());
        // 同一人不能有兩筆審核中的申請，所以改由沒有主管的 alice 申請讀取：條件不成立，第一關直接略過。
        (await session.RequestAsync(alice, "read")).EnsureSuccessStatusCode();

        Assert.Equal(1, (await InstanceAsync(bob, before)).CurrentStep);
        var after = (await TasksAsync(carol)).Single();
        var instance = await InstanceAsync(alice, after.Id);
        Assert.Equal(2, instance.CurrentStep);
        Assert.Equal((FlowStepStatuses.Skipped, FlowStepNotes.NotApplicable), (instance.Steps[0].Status, instance.Steps[0].Note));
    }

    [Fact]
    public async Task RulesArgumentsConditionsAndVersionsAreChecked()
    {
        await using var session = await RepositoryAccessSession.StartAsync();
        using var alice = await session.ClientAsync("alice");
        var version = (await DefinitionAsync(alice)).RowVersion;

        await AssertCodeAsync(await SaveAsync(alice, version, Step("審核", "no-such-rule")), HttpStatusCode.BadRequest, "flow_rule_invalid");
        await AssertCodeAsync(await SaveAsync(alice, version, Step("審核", "repository-stewards", """{"levels":1}""")), HttpStatusCode.BadRequest, "flow_argument_invalid");
        await AssertCodeAsync(await SaveAsync(alice, version, Step("主管", "applicant-manager", """{"levels":9}""")), HttpStatusCode.BadRequest, "flow_argument_invalid");
        await AssertCodeAsync(await SaveAsync(alice, version, Step("審核", "users", $$"""{"userIds":["{{Guid.NewGuid()}}"]}""")), HttpStatusCode.BadRequest, "flow_argument_invalid");
        await AssertCodeAsync(await SaveAsync(alice, version, Step("主管", "applicant-manager", """{"levels":1}""", "permission", "admin")), HttpStatusCode.BadRequest, "flow_condition_invalid");
        await AssertCodeAsync(await SaveAsync(alice, version), HttpStatusCode.BadRequest, "validation_failed");

        (await SaveAsync(alice, version, Step("負責人", "repository-stewards"))).EnsureSuccessStatusCode();
        await AssertCodeAsync(await SaveAsync(alice, version, Step("負責人", "repository-stewards")), HttpStatusCode.Conflict, "admin_write_conflict");
        Assert.Equal(["負責人"], (await DefinitionAsync(alice)).Steps.Select(x => x.Name));
    }

    [Fact]
    public async Task UsersStepsListTheReferencedPeopleByName()
    {
        await using var session = await RepositoryAccessSession.StartAsync();
        using var alice = await session.ClientAsync("alice");
        var dave = session.Users["dave"];

        (await SaveAsync(alice, (await DefinitionAsync(alice)).RowVersion, Step("指定審核", "users", $$"""{"userIds":["{{dave}}"]}"""))).EnsureSuccessStatusCode();

        var person = Assert.Single((await alice.GetFromJsonAsync<FlowDefinitionsDto>(Definitions))!.People);
        Assert.Equal(dave, person.Id);
    }

    [Fact]
    public async Task OnlyFlowAdministratorsSeeAndChangeDefinitions()
    {
        await using var session = await RepositoryAccessSession.StartAsync();
        using var bob = await session.ClientAsync("bob");

        Assert.Equal(HttpStatusCode.Forbidden, (await bob.GetAsync(Definitions)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SaveAsync(bob, 1, Step("負責人", "repository-stewards"))).StatusCode);
    }
}
