using AiNexus.Data.App.Library;

namespace AiNexus.Features.Library;

internal static class PromptTemplateQueries
{
    public static IQueryable<PromptTemplate> OwnedBy(this IQueryable<PromptTemplate> templates, Guid owner) => templates.Where(x => x.OwnerId == owner);
}
