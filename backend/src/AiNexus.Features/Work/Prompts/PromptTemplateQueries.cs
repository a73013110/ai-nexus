using AiNexus.Data.App.Prompts;

namespace AiNexus.Features.Work.Prompts;

internal static class PromptTemplateQueries
{
    public static IQueryable<PromptTemplate> OwnedBy(this IQueryable<PromptTemplate> templates, Guid owner) => templates.Where(x => x.OwnerId == owner);
}
