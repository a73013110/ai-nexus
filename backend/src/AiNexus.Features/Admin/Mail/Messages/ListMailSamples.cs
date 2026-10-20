using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.Mail.Messages;

/// <summary>
/// 寄信測試可選的一種系統信：範本與模型類別名、分類、優先序、是否敏感，以及模型參數的說明與範例值（虛構人名與目前設定），
/// 讓開發者照著寫 <c>outbox.EnqueueAsync(new TemplateType(), new ModelType(...), ...)</c>。
/// </summary>
public sealed record MailSampleDto(string Category, string Name, string Description, string Priority, bool Sensitive, string TemplateType, string ModelType,
    IReadOnlyList<MailSampleParameter> Parameters);

/// <summary>列出各模組登記的系統信（<see cref="MailSample"/>），設定測試信 <c>mail.test</c> 在最前面，其餘依分類排序。</summary>
internal sealed class ListMailSamples(IEnumerable<MailSample> samples, IOptions<MailOptions> options, TimeProvider clock)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("", (ListMailSamples handler) => handler.Handle())
        .WithName("ListMailSamples");

    public IReadOnlyList<MailSampleDto> Handle()
    {
        var context = Context(options.Value, clock);
        return samples.OrderBy(x => x.Category == TestMail.TestCategory ? 0 : 1).ThenBy(x => x.Category, StringComparer.Ordinal)
            .Select(x => new MailSampleDto(x.Category, x.Name, x.Description, x.Priority.ToString().ToLowerInvariant(), x.Sensitive, x.TemplateType, x.ModelType, x.Describe(context)))
            .ToList();
    }

    /// <summary>列表的範例值與實際寄出用同一份環境，看到的範例就是沒填時寄出的內容。</summary>
    internal static MailSampleContext Context(MailOptions settings, TimeProvider clock) => new(clock.GetUtcNow(), settings.Transport, settings.From.Address);
}
