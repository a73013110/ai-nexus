using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Library;

public sealed record SavePromptRequest(string Title, string Content);

internal sealed class SavePromptRequestValidator : RequestValidator<SavePromptRequest>
{
    public override string ProblemCode => "invalid_prompt_template";

    public SavePromptRequestValidator()
    {
        RuleFor(x => x.Title).Must(x => x.Trim().Length is >= 1 and <= PromptTemplate.TitleMaxLength).WithErrorCode("length");
        RuleFor(x => x.Content).Must(x => x.Trim().Length is >= 1 and <= PromptTemplate.ContentMaxLength).WithErrorCode("length");
    }
}

/// <summary>Creates a template, or edits one the user owns.</summary>
internal sealed class SavePromptTemplate(NexusDbContext db, TimeProvider clock)
{
    public static void Map(RouteGroupBuilder routes)
    {
        routes.MapPost("", (SavePromptRequest body, ICurrentUser user, SavePromptTemplate handler, CancellationToken ct) => handler.HandleAsync(user.Id, null, body, ct).ToHttpResultAsync())
            .WithName("CreatePromptTemplate");
        routes.MapPut("/{id:guid}", (Guid id, SavePromptRequest body, ICurrentUser user, SavePromptTemplate handler, CancellationToken ct) => handler.HandleAsync(user.Id, id, body, ct).ToHttpResultAsync())
            .WithName("UpdatePromptTemplate");
    }

    public async Task<Result<PromptTemplateDto>> HandleAsync(Guid owner, Guid? id, SavePromptRequest request, CancellationToken ct)
    {
        var templates = db.Set<PromptTemplate>();
        PromptTemplate? template;
        if (id is { } key)
        {
            template = await templates.OwnedBy(owner).SingleOrDefaultAsync(x => x.Id == key, ct);
            if (template is null) return LibraryErrors.NotFound;
        }
        else
        {
            if (await templates.OwnedBy(owner).CountAsync(ct) >= PromptTemplate.MaxPerOwner) return LibraryErrors.LimitReached;
            template = PromptTemplate.Create(owner);
            templates.Add(template);
        }
        template.Edit(request.Title, request.Content, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return template.ToDto();
    }
}
