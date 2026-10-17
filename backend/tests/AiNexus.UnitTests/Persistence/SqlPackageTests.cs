using AiNexus.Features.Persistence.ReleaseScripts;

namespace AiNexus.UnitTests.Persistence;

/// <summary>
/// SqlPackage 無法解析的 response-file 參數會連同密碼一起印出，所以跳脫規則必須與它的解析器一致：
/// 雙引號外的空白會切開參數，<c>\"</c> 代表引號，其他反斜線都是字面字元。
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
