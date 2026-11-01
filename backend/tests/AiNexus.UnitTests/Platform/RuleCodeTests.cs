using AiNexus.Platform.Validation;
using FluentValidation;

namespace AiNexus.UnitTests.Platform;

public sealed class RuleCodeTests
{
    [Theory]
    [InlineData("tooLong")]
    [InlineData("too-long")]
    [InlineData("_required")]
    [InlineData("required_")]
    [InlineData("")]
    public void RejectsCodesThatAreNotSnakeCase(string value) => Assert.Throws<ArgumentException>(() => new RuleCode(value));

    [Fact]
    public void FailuresCarryTheRuleCode()
    {
        var result = new TextValidator().Validate(new Text(" ", new string('x', 6) + "\n"));

        Assert.Equal(
            [("Required", "required"), ("Optional", "too_long"), ("Optional", "format")],
            result.Errors.Select(x => (x.PropertyName, x.ErrorCode)));
    }

    [Fact]
    public void TextLengthIsCountedAfterTrimming() => Assert.True(new TextValidator().Validate(new Text("  abcde  ", null)).IsValid);

    private sealed record Text(string? Required, string? Optional);

    private sealed class TextValidator : AbstractValidator<Text>
    {
        public TextValidator()
        {
            RuleFor(x => x.Required).RequiredText(5);
            RuleFor(x => x.Optional).OptionalText(5);
        }
    }
}
