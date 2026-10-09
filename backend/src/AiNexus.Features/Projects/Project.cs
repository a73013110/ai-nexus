using AiNexus.Features.Collaboration;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Projects;

/// <summary>Shared instructions and reference files for conversations. Its name, owner and sharing live on the <see cref="WorkspaceResource"/> with the same id.</summary>
public sealed class Project
{
    public const string Kind = "project";
    public const int MaxPerOwner = 100;
    public const int MaxFiles = 50;

    public Guid Id { get; set; }
    public string Description { get; set; } = "";
    public string Instructions { get; set; } = "";
    public int Version { get; set; } = 1;
    public bool IsArchived { get; set; }

    public ProjectDto ToDto(ResourceDto resource) => new(resource, Description, Instructions, Version, IsArchived);
}

public sealed record ProjectDto(ResourceDto Resource, string Description, string Instructions, int Version, bool IsArchived);

internal static class ProjectErrors
{
    public static readonly Error InvalidName = Error.Invalid("invalid_resource_name");
    public static readonly Error Limit = Error.Conflict("project_limit");
    public static readonly Error Conflict = Error.Conflict("project_conflict");
    public static readonly Error Archived = Error.Conflict("project_archived");
    public static readonly Error FileLimit = Error.Conflict("project_file_limit");
    public static readonly Error TemplateMissing = Error.NotFound("template_missing");
    public static readonly Error TemplateLimit = Error.Conflict("template_limit");
    public static readonly Error ChatAccessRequired = Error.Forbidden("chat_access_required");
    public static readonly Error AccessRequired = Error.Forbidden("project_access_required");
    public static readonly Error ConversationMissing = Error.NotFound("conversation_missing");
    public static readonly Error GenerationActive = Error.Conflict("generation_active");

    /// <summary>For <see cref="ProjectService"/>, whose callers in other modules can only fail by exception.</summary>
    public static ApiException ToException(this Error error)
    {
        var status = Problems.Status(error.Kind);
        return new(status, error.Code, PublicErrorCatalog.Message(error.Code, status));
    }
}

internal static class ProjectQueries
{
    /// <summary>The rule of <see cref="ResourceAccess.Name"/>.</summary>
    public static bool NameIsValid(string? name) => name?.Trim() is { Length: >= 1 and <= 120 } trimmed && !trimmed.Any(char.IsControl);

    /// <summary>The project as the actor may see it. Access failures stay exceptions of <see cref="ResourceAccess"/>.</summary>
    public static async Task<ProjectDto> LoadProjectAsync(this ResourceAccess access, NexusDbContext db, Guid actor, Guid id, CancellationToken ct)
    {
        var resource = await access.RequireAsync(actor, id, Project.Kind, ct);
        var project = await db.Set<Project>().AsNoTracking().SingleAsync(x => x.Id == id, ct);
        return project.ToDto(await access.DescribeAsync(actor, resource, ct));
    }

    /// <summary>Read (or edit) access first, then the project must not be archived. Access failures stay exceptions.</summary>
    public static async Task<Result<Project>> RequireActiveAsync(this ResourceAccess access, NexusDbContext db, Guid actor, Guid id, bool write, CancellationToken ct)
    {
        await access.RequireAsync(actor, id, Project.Kind, ct, write);
        var row = await db.Set<Project>().AsNoTracking().SingleAsync(x => x.Id == id, ct);
        if (row.IsArchived) return ProjectErrors.Archived;
        return row;
    }
}

/// <summary>Entry point kept for <c>NexusDbContext</c>.</summary>
public static class ProjectConfiguration
{
    public static void Configure(ModelBuilder model)
    {
        model.ApplyConfiguration(new ProjectEntityConfiguration());
        model.ApplyConfiguration(new ProjectTemplateConfiguration());
    }
}

internal sealed class ProjectEntityConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> row)
    {
        row.ToTable("Projects", "projects"); row.HasKey(x => x.Id);
        row.Property(x => x.Description).HasMaxLength(2000); row.Property(x => x.Instructions).HasMaxLength(4000);
    }
}
