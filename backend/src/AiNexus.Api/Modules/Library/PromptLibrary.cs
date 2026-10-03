using AiNexus.BuildingBlocks;
using AiNexus.Database;
using AiNexus.Modules.AccessControl;
using AiNexus.Modules.Identity;
using EDoc.Core.Database.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Modules.Library;

public sealed class PromptTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed record PromptTemplateDto(Guid Id, string Title, string Content, DateTimeOffset UpdatedAt);
public sealed record SavePromptRequest(string Title, string Content);

public static class LibraryConfiguration
{
    public static void Configure(ModelBuilder model)
    {
        var prompt = model.Entity<PromptTemplate>();
        prompt.ToTable("PromptTemplates", "library");
        prompt.HasKey(x => x.Id);
        prompt.Property(x => x.Title).HasMaxLength(80);
        prompt.Property(x => x.Content).HasMaxLength(12000);
        prompt.HasIndex(x => new { x.OwnerId, x.UpdatedAt });
        prompt.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PromptLibraryService(IEfHelper<INexusDatabase> ef)
{
    private static PromptTemplateDto Describe(PromptTemplate x) => new(x.Id, x.Title, x.Content, x.UpdatedAt);
    public async Task<IReadOnlyList<PromptTemplateDto>> ListAsync(Guid owner, CancellationToken ct)
        => (await ef.Set<PromptTemplate>().AsNoTracking().Where(x => x.OwnerId == owner).OrderByDescending(x => x.UpdatedAt).Take(100).ToListAsync(ct)).Select(Describe).ToList();
    public async Task<PromptTemplateDto> SaveAsync(Guid owner, Guid? id, SavePromptRequest request, CancellationToken ct)
    {
        var title = request.Title.Trim();
        var content = request.Content.Trim();
        if (title.Length is < 1 or > 80 || content.Length is < 1 or > 12000) throw new ApiException(400, "invalid_prompt_template", "範本標題最多 80 字元，內容最多 12,000 字元，皆不可空白。");
        PromptTemplate prompt;
        if (id is Guid key) prompt = await OwnedAsync(owner, key, ct);
        else
        {
            if (await ef.Set<PromptTemplate>().CountAsync(x => x.OwnerId == owner, ct) >= 100) throw new ApiException(400, "template_limit", "個人範本最多 100 個。");
            prompt = new() { OwnerId = owner };
            ef.Set<PromptTemplate>().Add(prompt);
        }
        prompt.Title = title;
        prompt.Content = content;
        prompt.UpdatedAt = DateTimeOffset.UtcNow;
        await ef.SaveChangesAsync(ct);
        return Describe(prompt);
    }
    public async Task DeleteAsync(Guid owner, Guid id, CancellationToken ct)
    {
        ef.Set<PromptTemplate>().Remove(await OwnedAsync(owner, id, ct));
        await ef.SaveChangesAsync(ct);
    }
    private async Task<PromptTemplate> OwnedAsync(Guid owner, Guid id, CancellationToken ct)
        => await ef.Set<PromptTemplate>().SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == owner, ct) ?? throw new ApiException(404, "template_not_found", "找不到這個範本。");
}

public static class PromptLibraryEndpoints
{
    public static void MapPromptLibrary(this RouteGroupBuilder api)
    {
        var routes = api.MapGroup("/prompt-templates").RequireAuthorization(BuiltInAccess.ChatPolicy).WithTags("PromptLibrary");
        routes.MapGet("", async (CurrentUser current, PromptLibraryService library, CancellationToken ct) => Results.Ok(await library.ListAsync((await current.GetAsync(ct)).Id, ct))).WithName("ListPromptTemplates").Produces<IReadOnlyList<PromptTemplateDto>>();
        routes.MapPost("", async (SavePromptRequest body, CurrentUser current, PromptLibraryService library, CancellationToken ct) => Results.Ok(await library.SaveAsync((await current.GetAsync(ct)).Id, null, body, ct))).WithName("CreatePromptTemplate").Produces<PromptTemplateDto>();
        routes.MapPut("/{id:guid}", async (Guid id, SavePromptRequest body, CurrentUser current, PromptLibraryService library, CancellationToken ct) => Results.Ok(await library.SaveAsync((await current.GetAsync(ct)).Id, id, body, ct))).WithName("UpdatePromptTemplate").Produces<PromptTemplateDto>();
        routes.MapDelete("/{id:guid}", async (Guid id, CurrentUser current, PromptLibraryService library, CancellationToken ct) => { await library.DeleteAsync((await current.GetAsync(ct)).Id, id, ct); return Results.NoContent(); }).WithName("DeletePromptTemplate").Produces(204);
    }
}
