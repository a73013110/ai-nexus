using AiNexus.Features.Persistence.ReleaseScripts;

namespace AiNexus.UnitTests.Persistence;

/// <summary>
/// SqlPackage prints a response-file argument it cannot parse, password included, so the quoting must match its parser:
/// white space splits outside double quotes, <c>\"</c> is a quote, other backslashes are literal.
/// </summary>
public sealed class SqlPackageTests
{
    [Theory]
    [InlineData("Server=sql;Password=p a;", "/SourceConnectionString:\"Server=sql;Password=p a;\"")]
    [InlineData("Password=\"x\"y", "/SourceConnectionString:\"Password=\\\"x\\\"y\"")]
    [InlineData(@"Password=a\b", "/SourceConnectionString:\"Password=a\\b\"")]
    [InlineData(@"Password=a\", "/SourceConnectionString:\"Password=a\\;\"")]
    public void ResponseFileArgumentIsOneQuotedToken(string value, string expected)
    {
        Assert.Equal(expected, SqlPackage.ResponseFileArgument("SourceConnectionString", value));
    }

    [Fact]
    public void ResponseFileArgumentRejectsLineBreaks()
    {
        Assert.Throws<ReleaseScriptException>(() => SqlPackage.ResponseFileArgument("SourceConnectionString", "Password=a\nb"));
    }
}
