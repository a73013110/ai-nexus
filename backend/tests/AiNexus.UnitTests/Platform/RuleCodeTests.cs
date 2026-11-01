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
            result.Errors.Select(x => (x.PropertyName, RuleCode.Resolve(x.ErrorCode))));
    }

    [Fact]
    public void TextLengthIsCountedAfterTrimming() => Assert.True(new TextValidator().Validate(new Text("  abcde  ", null)).IsValid);

    // 內建規則與沒指定代碼的 Must 都對應到同一套代碼，不外露 FluentValidation 的類別名稱。
    [Fact]
    public void BuiltInRulesResolveToTheSharedVocabulary()
    {
        var result = new BuiltInValidator().Validate(new Values(null, new string('x', 6), 0, "x", "A"));

        Assert.Equal(
            [("Required", "required"), ("Long", "too_long"), ("Number", "range"), ("Absent", "not_allowed"), ("Checked", "invalid")],
            result.Errors.Select(x => (x.PropertyName, RuleCode.Resolve(x.ErrorCode))));
    }

    [Fact]
    public void SpecifiedCodesPassThrough() => Assert.Equal("too_many", RuleCode.Resolve(RuleCode.TooMany.Value));

    private sealed record Text(string? Required, string? Optional);

    private sealed record Values(string? Required, string Long, int Number, string? Absent, string Checked);

    private sealed class BuiltInValidator : AbstractValidator<Values>
    {
        public BuiltInValidator()
        {
            RuleFor(x => x.Required).NotEmpty();
            RuleFor(x => x.Long).MaximumLength(5);
            RuleFor(x => x.Number).InclusiveBetween(1, 3);
            RuleFor(x => x.Absent).Null();
            RuleFor(x => x.Checked).Must(x => x == "a");
        }
    }

    private sealed class TextValidator : AbstractValidator<Text>
    {
        public TextValidator()
        {
            RuleFor(x => x.Required).RequiredText(5);
            RuleFor(x => x.Optional).OptionalText(5);
        }
    }
}
