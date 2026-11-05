using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;

namespace AiNexus.Features.Platform.Assistant;

/// <summary>同一個面板裡先前的一問一答；只活在瀏覽器，伺服器不保存，每次提問一起送來當上下文。</summary>
public sealed record AssistantTurn(string Question, string Answer);

/// <summary>
/// 對頁內助理的一次提問。<paramref name="ModelId"/> 為 null 時用使用者可用的預設模型（與對話相同的模型政策）；
/// <paramref name="History"/> 是同一個面板先前的輪次，舊的在前，放不進模型上下文時由 <see cref="AssistantResponder"/> 從最舊的捨去。
/// </summary>
public sealed record AssistantQuestion(string Text, string? ModelId = null, IReadOnlyList<AssistantTurn>? History = null);

/// <summary>提問與先前輪次只限長度與輪數：和對話的提問一樣原樣交給模型。</summary>
internal sealed class AssistantQuestionValidator : RequestValidator<AssistantQuestion>
{
    public const int MaxQuestionCharacters = 500, MaxAnswerCharacters = 4000, MaxTurns = 6;

    /// <summary>一次提問最多的文字量（模型代號另計，很短）。</summary>
    public const int MaxCharacters = MaxQuestionCharacters + MaxTurns * (MaxQuestionCharacters + MaxAnswerCharacters) + 256;

    public override Error Problem => AssistantErrors.QuestionInvalid;

    public AssistantQuestionValidator()
    {
        RuleFor(x => x.Text).Must(x => !string.IsNullOrWhiteSpace(x)).WithErrorCode(RuleCode.Required).MaximumLength(MaxQuestionCharacters);
        RuleFor(x => x.ModelId).MaximumLength(200);
        RuleFor(x => x.History).Must(x => x is null || x.Count <= MaxTurns).WithErrorCode(RuleCode.TooMany);
        RuleForEach(x => x.History).ChildRules(turn =>
        {
            turn.RuleFor(x => x.Question).NotEmpty().MaximumLength(MaxQuestionCharacters);
            turn.RuleFor(x => x.Answer).NotEmpty().MaximumLength(MaxAnswerCharacters);
        });
    }
}
