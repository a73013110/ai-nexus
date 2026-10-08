using System.Text.RegularExpressions;
using AiNexus.Features.AccessControl;
using AiNexus.Features.Identity;
using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Administration;

public sealed class AdministrationOptions { public string[] BootstrapAdministrators { get; set; } = []; }
public sealed class AdministrativeWriteLock { public SemaphoreSlim Gate { get; } = new(1, 1); }
public sealed class AdministratorBootstrap
{
    public Guid UserId { get; set; }
    public DateTimeOffset GrantedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>The administrator role, group and feature, and the module's tables. <see cref="Configure"/> is the entry point kept for <c>NexusDbContext</c>.</summary>
public static class AdministrationConfiguration
{
    public const string Role = "administrator", Group = "administrators", Feature = "admin", Policy = Policies.Prefix + Feature;
    public static void Configure(ModelBuilder model)
    {
        model.Entity<AccessControl.Role>().HasData(new AccessControl.Role { Id = Role, Name = "平台管理員" });
        model.Entity<RoleGroup>().HasData(new RoleGroup { Id = Group, Name = "平台管理" });
        model.Entity<AccessControl.Feature>().HasData(new AccessControl.Feature { Id = Feature, Name = "平台管理", Route = "/admin", SortOrder = 90 });
        model.Entity<RoleGroupRole>().HasData(new RoleGroupRole { RoleId = Role, GroupId = Group });
        model.Entity<RoleGroupFeature>().HasData(new RoleGroupFeature { GroupId = Group, FeatureId = Feature });
        model.ApplyConfiguration(new AdministratorBootstrapConfiguration());
        model.ApplyConfiguration(new GroupModelPolicyConfiguration());
        model.ApplyConfiguration(new UserModelPolicyConfiguration());
    }
}

internal sealed class AdministratorBootstrapConfiguration : IEntityTypeConfiguration<AdministratorBootstrap>
{
    public void Configure(EntityTypeBuilder<AdministratorBootstrap> bootstrap)
    {
        bootstrap.ToTable("AdministratorBootstraps", "access"); bootstrap.HasKey(x => x.UserId);
        bootstrap.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal static class AdministrationErrors
{
    public static readonly Error AdminRequired = Error.Forbidden("admin_required");
    public static readonly Error Lockout = Error.Conflict("admin_lockout");
    public static readonly Error NotFound = Error.NotFound("admin_resource_not_found");
    public static readonly Error InvalidSearch = Error.Invalid("invalid_search");
    public static readonly Error InvalidStorageLimit = Error.Invalid("invalid_storage_limit");
    public static readonly Error InvalidModelPolicy = Error.Invalid("invalid_model_policy");
    public static readonly Error InvalidAccessId = Error.Invalid("invalid_access_id");
    public static readonly Error InvalidAccessIds = Error.Invalid("invalid_access_ids");
    public static readonly Error InvalidAccessName = Error.Invalid("invalid_access_name");
    public static readonly Error UnknownAccessId = Error.Invalid("unknown_access_id");
    public static readonly Error InvalidOrder = Error.Invalid("invalid_order");

    /// <summary>For <see cref="AdministrativeAudit.MutateAsync"/>, whose callers in other modules can only fail by exception.</summary>
    public static ApiException ToException(this Error error)
    {
        var status = Problems.Status(error.Kind);
        return new(status, error.Code, PublicErrorCatalog.Message(error.Code, status));
    }
}

/// <summary>
/// Format rules for role, group and feature edits. They run inside <see cref="AdministrativeAudit.TryMutateAsync"/>, after
/// the administrator check, so a rejected edit is still audited with its code; that is why they are not request validators.
/// </summary>
internal static class AccessRules
{
    public static Error? Key(string id) => Regex.IsMatch(id, "^[a-z][a-z0-9_-]{0,63}$", RegexOptions.CultureInvariant) ? null : AdministrationErrors.InvalidAccessId;

    public static Error? Keys(IReadOnlyList<string> ids)
    {
        if (ids.Count > 50 || ids.Distinct().Count() != ids.Count) return AdministrationErrors.InvalidAccessIds;
        foreach (var id in ids) if (Key(id) is { } invalid) return invalid;
        return null;
    }

    public static Error? Name(string name) => string.IsNullOrWhiteSpace(name) || name.Trim().Length > 120 || name.Any(char.IsControl) ? AdministrationErrors.InvalidAccessName : null;

    public static async Task<Error?> ExistingAsync(IQueryable<string> query, IReadOnlyList<string> ids, CancellationToken ct)
        => await query.CountAsync(x => ids.Contains(x), ct) != ids.Count ? AdministrationErrors.UnknownAccessId : null;
}
