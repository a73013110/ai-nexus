using AiNexus.Features.Admin.Scheduling;
using AiNexus.Features.Admin.Scheduling.Engine;
using AiNexus.Platform.Scheduling;
using Microsoft.Extensions.Options;

namespace AiNexus.UnitTests.Scheduling;

public sealed class ScheduledTaskCatalogTests
{
    private static readonly ScheduledTaskRegistration Recurring = Register(new("test.recurring", "定期", "測試", "定期任務。"));
    private static readonly ScheduledTaskRegistration Startup = Register(new("test.startup", "啟動", "測試", "啟動任務。") { FixedSchedule = Schedule.AtStartup(TimeSpan.FromMinutes(1)) });

    [Fact]
    public void EveryTaskTakesItsScheduleAndTimeoutFromSettings()
    {
        var catalog = Build(new()
        {
            Execute = true,
            ExecuteOnly = ["test.startup"],
            Tasks =
            {
                ["test.recurring"] = new() { Schedule = "30 3 * * *", Timeout = TimeSpan.FromMinutes(10), Enabled = false },
                ["test.startup"] = new() { Timeout = TimeSpan.FromMinutes(5) },
            },
        });

        var recurring = catalog.Find("test.recurring")!;
        Assert.Equal(("每天 03:30", TimeSpan.FromMinutes(10), false, false), (recurring.Schedule.Description, recurring.Timeout, recurring.Enabled, recurring.ExecutesHere));
        var startup = catalog.Find("test.startup")!;
        Assert.Equal((true, true), (startup.Schedule.RunsAtStartup, startup.ExecutesHere));
    }

    [Fact]
    public void IncompleteOrUnknownSettingsFailStartupListingEveryProblem()
    {
        var error = Assert.Throws<InvalidOperationException>(() => Build(new()
        {
            ExecuteOnly = ["test.missing"],
            Tasks =
            {
                ["test.recurring"] = new() { Timeout = TimeSpan.FromMinutes(10) },
                ["test.startup"] = new() { Schedule = "00:05:00" },
                ["test.unknown"] = new() { Schedule = "00:05:00", Timeout = TimeSpan.FromMinutes(10) },
            },
        }));

        Assert.Contains("Scheduling:Tasks:test.recurring:Schedule is required", error.Message, StringComparison.Ordinal);
        Assert.Contains("Scheduling:Tasks:test.startup:Schedule cannot be set", error.Message, StringComparison.Ordinal);
        Assert.Contains("Scheduling:Tasks:test.startup:Timeout is required", error.Message, StringComparison.Ordinal);
        Assert.Contains("Scheduling:Tasks:test.unknown is not a registered task", error.Message, StringComparison.Ordinal);
        Assert.Contains("'test.missing'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ATaskMissingFromSettingsFailsStartup()
    {
        var error = Assert.Throws<InvalidOperationException>(() => Build(new() { Tasks = { ["test.startup"] = new() { Timeout = TimeSpan.FromMinutes(5) } } }));
        Assert.Contains("Scheduling:Tasks:test.recurring is missing", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnreadableSettingsAreRejectedByTheValidator()
    {
        var options = new SchedulingOptions { Tasks = { ["test.recurring"] = new() { Schedule = "every hour", Timeout = TimeSpan.FromDays(2) } } };
        var result = new SchedulingOptionsValidator().Validate(null, options);
        Assert.True(result.Failed);
        Assert.Contains("Tasks:test.recurring:Schedule", result.FailureMessage, StringComparison.Ordinal);
        Assert.Contains("Tasks:test.recurring:Timeout", result.FailureMessage, StringComparison.Ordinal);
    }

    private static ScheduledTaskCatalog Build(SchedulingOptions options) => new([Recurring, Startup], Options.Create(options));

    private static ScheduledTaskRegistration Register(ScheduledTaskDefinition definition) => new(definition, (_, _, _) => Task.FromResult(TaskOutcome.Done()));
}
