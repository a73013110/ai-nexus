using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.AccessControl;

public static class PlatformFeatures
{
    public static void Add(ModelBuilder model, string id, string name, string route, int order, bool administratorsOnly = false)
    {
        model.Entity<Feature>().HasData(new Feature { Id = id, Name = name, Route = route, SortOrder = order });
        model.Entity<RoleGroupFeature>().HasData(new RoleGroupFeature { GroupId = administratorsOnly ? "administrators" : BuiltInAccess.WorkspaceGroup, FeatureId = id });
    }
}
