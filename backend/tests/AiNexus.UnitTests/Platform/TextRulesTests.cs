using AiNexus.Platform.Validation;
using FluentValidation;

namespace AiNexus.UnitTests.Platform;

public sealed class TextRulesTests
{
    [Fact]
    public void FailuresCarryTheSharedCodes()
    {
        var result = new TextValidator().Validate(new Text(" ", new string('x', 6) + "\n", "", "a\u0007"));

        Assert.Equal(
            [("Line", "required"), ("OptionalLine", "too_long"), ("OptionalLine", "format"), ("Content", "required"), ("OptionalContent", "format")],
            result.Errors.Select(x => (x.PropertyName, RuleCode.Resolve(x.ErrorCode))));
    }

    [Fact]
    public void LengthIsCountedAfterTrimming() => Assert.True(new TextValidator().Validate(new Text("  abcde  ", null, "  abcde  ", null)).IsValid);

    // 內容允許換行與 Tab；單行連換行都不允許。
    [Fact]
    public void ContentAllowsLineBreaksButLinesDoNot()
    {
        Assert.True(TextRules.IsContent("a\r\n\tb", 10));
        Assert.False(TextRules.IsLine("a\nb", 10));
        Assert.False(TextRules.IsContent("a\u0085b", 10));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("abcdef")]
    public void RequiredTextRejectsMissingOrLongValues(string? value)
    {
        Assert.False(TextRules.IsLine(value, 5));
        Assert.False(TextRules.IsContent(value, 5));
    }

    private sealed record Text(string? Line, string? OptionalLine, string? Content, string? OptionalContent);

    private sealed class TextValidator : AbstractValidator<Text>
    {
        public TextValidator()
        {
            RuleFor(x => x.Line).RequiredText(5);
            RuleFor(x => x.OptionalLine).OptionalText(5);
            RuleFor(x => x.Content).RequiredContent(5);
            RuleFor(x => x.OptionalContent).OptionalContent(5);
        }
    }
}
