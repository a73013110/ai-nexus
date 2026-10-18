using AiNexus.Host.Commands;

namespace AiNexus.UnitTests.Host;

public sealed class HostCommandTests
{
    [Fact]
    public void NoCommandServesWithAllArgumentsAsConfiguration()
    {
        var command = HostCommand.Parse(["--urls", "http://localhost:5080", "--LocalConfigPath", "a.json"]);
        Assert.Null(command.Name);
        Assert.Null(command.Error);
        Assert.False(command.Help);
        Assert.Equal(["--urls", "http://localhost:5080", "--LocalConfigPath", "a.json"], command.HostArguments);
    }

    [Theory]
    [InlineData("verify deployment")]
    [InlineData("verify connections")]
    [InlineData("verify sql")]
    public void CommandWordsAreRemovedBeforeConfiguration(string name)
    {
        var command = HostCommand.Parse([.. name.Split(' '), "--contentRoot", "app", "--SecretsConfigPath", "s.json"]);
        Assert.Equal(name, command.Name);
        Assert.Null(command.Error);
        Assert.Equal(["--contentRoot", "app", "--SecretsConfigPath", "s.json"], command.HostArguments);
        Assert.Contains(name, HostCommand.Usage);
    }

    [Fact]
    public void CommandMayFollowOptions()
    {
        var command = HostCommand.Parse(["--contentRoot", "app", "--LocalConfigPath=a.json", "verify", "sql"]);
        Assert.Equal("verify sql", command.Name);
        Assert.Equal(["--contentRoot", "app", "--LocalConfigPath=a.json"], command.HostArguments);
    }

    [Theory]
    [InlineData("--output", "report.json")]
    [InlineData("--output=report.json")]
    public void VerifyReportPathIsNotConfiguration(params string[] output)
    {
        var command = HostCommand.Parse(["verify", "sql", "--LocalConfigPath", "a.json", .. output]);
        Assert.Equal("report.json", command.Output);
        Assert.Equal(["--LocalConfigPath", "a.json"], command.HostArguments);
    }

    [Theory]
    [InlineData("db")]
    [InlineData("db init")]
    [InlineData("db migrate")]
    [InlineData("db script")]
    [InlineData("db diff")]
    [InlineData("serve")]
    [InlineData("verify")]
    public void UnknownCommandIsAUsageError(string name)
    {
        var command = HostCommand.Parse(name.Split(' '));
        Assert.Null(command.Name);
        Assert.Contains(name, command.Error);
    }

    [Fact]
    public void StrayWordAfterOptionsIsAUsageError()
    {
        var command = HostCommand.Parse(["--urls", "http://localhost", "verify", "sql", "extra"]);
        Assert.Null(command.Name);
        Assert.Contains("verify sql extra", command.Error);
    }

    [Theory]
    [InlineData("verify", "deployment", "--output", "x.json")]
    [InlineData("verify", "connections", "--output")]
    [InlineData("verify", "connections", "--output", "--urls")]
    public void InvalidOutputIsAUsageError(params string[] args)
    {
        var command = HostCommand.Parse(args);
        Assert.Null(command.Name);
        Assert.NotNull(command.Error);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("verify", "sql", "-h")]
    public void HelpNeverStartsTheHost(params string[] args)
    {
        var command = HostCommand.Parse(args);
        Assert.True(command.Help);
        Assert.Null(command.Name);
    }
}
