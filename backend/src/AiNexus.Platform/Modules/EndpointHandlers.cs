using System.Reflection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AiNexus.Platform.Modules;

/// <summary>
/// A use case is one <c>internal sealed class</c>: static <c>Map</c> methods declare its endpoints and the instance
/// <c>HandleAsync</c> does the work, with dependencies taken by the constructor. The endpoint lambda receives the class
/// itself as a parameter, so it is registered here by that shape and a new slice needs no registration line.
/// </summary>
public static class EndpointHandlers
{
    public const string HandleMethod = "HandleAsync";

    public static IServiceCollection AddEndpointHandlers(this IServiceCollection services, Assembly assembly)
    {
        foreach (var slice in Slices(assembly).Where(type => !type.IsAbstract)) services.TryAddScoped(slice);
        return services;
    }

    /// <summary>Types that map endpoints onto a module's route group.</summary>
    public static IEnumerable<Type> Slices(Assembly assembly) => assembly.GetTypes().Where(type => MapMethods(type).Any());

    public static IEnumerable<MethodInfo> MapMethods(Type type) => type
        .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly)
        .Where(method => method.Name.StartsWith("Map", StringComparison.Ordinal) && !method.IsGenericMethodDefinition
            && method.GetParameters() is [{ ParameterType: var routes }, ..] && routes == typeof(RouteGroupBuilder))
        .Where(_ => !typeof(IFeatureModule).IsAssignableFrom(type));
}
