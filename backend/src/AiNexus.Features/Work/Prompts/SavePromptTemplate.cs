using AiNexus.Data.App;
using AiNexus.Data.App.Prompts;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Prompts;

public sealed record SavePromptRequest(string Title, string Content);

internal sealed class SavePromptRequestValidator : RequestValidator<SavePromptRequest>
{
    public override Error Problem => PromptsErrors.Invalid;

    public SavePromptRequestValidator()
    {
        RuleFor(x => x.Title).RequiredText(PromptTemplate.TitleMaxLength);
        RuleFor(x => x.Content).RequiredContent(PromptTemplate.ContentMaxLength);
    }
}

/// <summary>建立範本，或編輯使用者自己擁有的範本。</summary>
internal sealed class SavePromptTemplate(AppDbContext db, TimeProvider clock)
{
    public static void Map(RouteGroupBuilder routes)
    {
        routes.MapPost("", (SavePromptRequest body, ICurrentUser user, SavePromptTemplate handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, null, body, ct))
            .Audited(PromptsAudit.TemplateCreated)
            .WithName("CreatePromptTemplate");
        routes.MapPut("/{id:guid}", (Guid id, SavePromptRequest body, ICurrentUser user, SavePromptTemplate handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, id, body, ct))
            .Audited(PromptsAudit.TemplateUpdated)
            .WithName("UpdatePromptTemplate");
    }

    public async Task<Result<PromptTemplateDto>> HandleAsync(Guid owner, Guid? id, SavePromptRequest request, CancellationToken ct)
    {
        PromptTemplate? template;
        if (id is { } key)
        {
            template = await db.PromptTemplates.OwnedBy(owner).SingleOrDefaultAsync(x => x.Id == key, ct);
            if (template is null) return PromptsErrors.NotFound;
        }
        else
        {
            if (await db.PromptTemplates.OwnedBy(owner).CountAsync(ct) >= PromptTemplate.MaxPerOwner) return PromptsErrors.LimitReached;
            template = PromptTemplate.Create(owner);
            db.PromptTemplates.Add(template);
        }
        template.Edit(request.Title, request.Content, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return template.ToDto();
    }
}
