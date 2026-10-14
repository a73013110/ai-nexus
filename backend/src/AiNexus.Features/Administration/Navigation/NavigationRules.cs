using System.Text.RegularExpressions;
using AiNexus.Features.AccessControl.Navigation;
using AiNexus.Features.Administration.Roles;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Administration.Navigation;

/// <summary>
/// Format and placement rules for menu-group and feature-placement edits. Like <see cref="AccessRules"/> they run inside the
/// audited administrative transaction, so a rejected edit is still audited with its code.
/// </summary>
internal static class NavigationRules
{
    public const int MaxSortOrder = 10000;

    public static Error? Order(int sortOrder) => sortOrder is < 0 or > MaxSortOrder ? AdministrationErrors.InvalidOrder : null;

    public static Error? Icon(string icon) => Regex.IsMatch(icon, "^[a-z][a-z0-9-]{0,39}$", RegexOptions.CultureInvariant) ? null : AdministrationErrors.InvalidFeatureIcon;

    /// <summary>A null group is the top level; any other id must name an existing group.</summary>
    public static async Task<Error?> ExistingGroupAsync(NexusDbContext db, string? id, CancellationToken ct)
        => id is null || await db.Set<NavigationGroup>().AnyAsync(x => x.Id == id, ct) ? null : AdministrationErrors.NavigationGroupNotFound;
}
