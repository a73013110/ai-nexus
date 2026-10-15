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
    [InlineData("db init")]
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
        var command = HostCommand.Parse(["--contentRoot", "app", "--LocalConfigPath=a.json", "db", "init"]);
        Assert.Equal("db init", command.Name);
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
    [InlineData("db migrate")]
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
        var command = HostCommand.Parse(["--urls", "http://localhost", "db", "init", "extra"]);
        Assert.Null(command.Name);
        Assert.Contains("db init extra", command.Error);
    }

    [Theory]
    [InlineData("db", "init", "--output", "x.json")]
    [InlineData("verify", "connections", "--output")]
    [InlineData("verify", "connections", "--output", "--urls")]
    public void InvalidOutputIsAUsageError(params string[] args)
    {
        var command = HostCommand.Parse(args);
        Assert.Null(command.Name);
        Assert.NotNull(command.Error);
    }

    [Theory]
    [InlineData("--to=20261015031630_RemoveEmbeddingProfileChunkerConfiguration")]
    [InlineData("--to", "20261015031630_RemoveEmbeddingProfileChunkerConfiguration")]
    public void ReleaseScriptOptionsAreNotConfiguration(params string[] to)
    {
        var command = HostCommand.Parse(["db", "script", "--from", "0", .. to, "--output=release/database", "--LocalConfigPath", "a.json"]);
        Assert.Null(command.Error);
        Assert.Equal("db script", command.Name);
        Assert.Equal("0", command.Option("--from"));
        Assert.Equal("20261015031630_RemoveEmbeddingProfileChunkerConfiguration", command.Option("--to"));
        Assert.Equal("release/database", command.Output);
        Assert.Equal(["--LocalConfigPath", "a.json"], command.HostArguments);
        Assert.Contains("--from <migration>", HostCommand.Usage);
    }

    [Theory]
    [InlineData("--from", "db", "script", "--output", "out")]
    [InlineData("--output", "db", "script", "--from", "0")]
    [InlineData("--from", "db", "script", "--output", "out", "--from")]
    [InlineData("--from", "db", "init", "--from", "0")]
    [InlineData("--to", "verify", "sql", "--to", "x")]
    public void ReleaseScriptOptionErrorsNameTheOption(string option, params string[] args)
    {
        var command = HostCommand.Parse(args);
        Assert.Null(command.Name);
        Assert.Contains(option, command.Error);
    }

    [Fact]
    public void SchemaDiffOptionsAreNotConfiguration()
    {
        var command = HostCommand.Parse(["db", "diff", "--from", "20261014140318_NavigationGroups", "--output", "package/database", "--sqlpackage", @"C:\tools\SqlPackage.exe", "--LocalConfigPath", "a.json"]);
        Assert.Null(command.Error);
        Assert.Equal("db diff", command.Name);
        Assert.Equal("20261014140318_NavigationGroups", command.Option("--from"));
        Assert.Null(command.Option("--to"));
        Assert.Equal("package/database", command.Output);
        Assert.Equal(@"C:\tools\SqlPackage.exe", command.Option("--sqlpackage"));
        Assert.Equal(["--LocalConfigPath", "a.json"], command.HostArguments);
        Assert.Contains("--sqlpackage <檔案>", HostCommand.Usage);
    }

    [Theory]
    [InlineData("--output", "db", "diff", "--from", "0")]
    [InlineData("--from", "db", "diff", "--output", "out")]
    [InlineData("--sqlpackage", "db", "script", "--from", "0", "--output", "out", "--sqlpackage", "x")]
    public void SchemaDiffOptionErrorsNameTheOption(string option, params string[] args)
    {
        var command = HostCommand.Parse(args);
        Assert.Null(command.Name);
        Assert.Contains(option, command.Error);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("db", "init", "-h")]
    public void HelpNeverStartsTheHost(params string[] args)
    {
        var command = HostCommand.Parse(args);
        Assert.True(command.Help);
        Assert.Null(command.Name);
    }
}
