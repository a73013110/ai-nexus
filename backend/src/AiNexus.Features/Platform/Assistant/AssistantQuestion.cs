using System.Text.Json;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;

namespace AiNexus.Features.Platform.Assistant;

/// <summary>同一個面板裡先前的一問一答；只活在瀏覽器，伺服器不保存，每次提問一起送來當上下文。</summary>
public sealed record AssistantTurn(string Question, string Answer);

/// <summary>
/// 對助理的一次提問。<paramref name="ModelId"/> 為 null 時用使用者可用的預設模型（與對話相同的模型政策）；
/// <paramref name="History"/> 是同一個面板先前的輪次，舊的在前，放不進模型上下文時從最舊的捨去。
/// <paramref name="Page"/> 是提問所在的頁面（<c>FeatureCatalog</c> 的 ID），<paramref name="View"/> 是那一頁目前的查詢條件（與頁面查詢端點的參數同名），
/// 交給描述那一頁的工具（<see cref="IAssistantTool.DescribesPage"/>）。
/// </summary>
public sealed record AssistantQuestion(string Text, string? ModelId = null, IReadOnlyList<AssistantTurn>? History = null, string? Page = null, JsonElement? View = null);

/// <summary>提問與先前輪次只限長度與輪數：和對話的提問一樣原樣交給模型。畫面條件由工具依自己的查詢規則驗證。</summary>
internal sealed class AssistantQuestionValidator : RequestValidator<AssistantQuestion>
{
    public const int MaxQuestionCharacters = 500, MaxAnswerCharacters = 4000, MaxTurns = 6, MaxViewCharacters = 2000;

    /// <summary>一次提問最多的文字量（模型代號、頁面另計，很短）。</summary>
    public const int MaxCharacters = MaxQuestionCharacters + MaxTurns * (MaxQuestionCharacters + MaxAnswerCharacters) + MaxViewCharacters + 512;

    public override Error Problem => AssistantErrors.QuestionInvalid;

    public AssistantQuestionValidator()
    {
        RuleFor(x => x.Text).Must(x => !string.IsNullOrWhiteSpace(x)).WithErrorCode(RuleCode.Required).MaximumLength(MaxQuestionCharacters);
        RuleFor(x => x.ModelId).MaximumLength(200);
        RuleFor(x => x.Page).MaximumLength(100);
        // 畫面條件是平面的物件（參數名 → 值），太大或不是物件就不是頁面送來的。
        RuleFor(x => x.View).Must(x => x is null || x.Value.ValueKind == JsonValueKind.Object && x.Value.GetRawText().Length <= MaxViewCharacters)
            .WithErrorCode(RuleCode.Invalid);
        RuleFor(x => x.History).Must(x => x is null || x.Count <= MaxTurns).WithErrorCode(RuleCode.TooMany);
        RuleForEach(x => x.History).ChildRules(turn =>
        {
            turn.RuleFor(x => x.Question).NotEmpty().MaximumLength(MaxQuestionCharacters);
            turn.RuleFor(x => x.Answer).NotEmpty().MaximumLength(MaxAnswerCharacters);
        });
    }
}
