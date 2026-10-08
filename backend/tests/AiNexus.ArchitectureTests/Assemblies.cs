using System.Reflection;

namespace AiNexus.ArchitectureTests;

internal static class Assemblies
{
    public static readonly Assembly Platform = typeof(AiNexus.Platform.Modules.IFeatureModule).Assembly;
    public static readonly Assembly Features = typeof(AiNexus.Features.FeatureModules).Assembly;
}
