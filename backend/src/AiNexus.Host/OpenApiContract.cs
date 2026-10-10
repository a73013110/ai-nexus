using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace AiNexus.Host;

/// <summary>
/// The OpenAPI document is the frontend's only source of API types (contracts/openapi.json → schema.ts), so it must say
/// exactly which properties a response always carries.
/// </summary>
internal static class OpenApiContract
{
    public static void AddOpenApiContract(this IServiceCollection services) => services.AddOpenApi(options =>
    {
        options.AddDocumentTransformer((document, _, _) =>
        {
            document.Servers = [new() { Url = "/" }];
            return Task.CompletedTask;
        });
        // [AsParameters] classes contribute their PascalCase property names; binding ignores case and every other API
        // name is camelCase, so the contract (and the generated client) uses camelCase for these too.
        options.AddOperationTransformer((operation, _, _) =>
        {
            foreach (var parameter in operation.Parameters?.OfType<OpenApiParameter>() ?? [])
                if (parameter.In == ParameterLocation.Query && parameter.Name is { } name) parameter.Name = JsonNamingPolicy.CamelCase.ConvertName(name);
            return Task.CompletedTask;
        });
        options.AddSchemaTransformer<ResponseSchemaRequirements>();
    });
}

/// <summary>
/// The generated schema only marks constructor parameters without a default value as required, yet the server writes
/// every property of a response (there is no global null skipping). Types that are only ever returned therefore mark
/// every property that is always written as required; a property with a conditional <c>JsonIgnore</c> stays optional.
/// Types that are also accepted as a request body keep the generated list, because clients may omit defaulted values.
/// </summary>
internal sealed class ResponseSchemaRequirements(IApiDescriptionGroupCollectionProvider apis) : IOpenApiSchemaTransformer
{
    private HashSet<Type>? requestTypes;

    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var type = context.JsonTypeInfo;
        if (type.Kind != JsonTypeInfoKind.Object || schema.Properties is not { Count: > 0 } properties) return Task.CompletedTask;
        requestTypes ??= RequestTypes(type.Options);
        if (requestTypes.Contains(type.Type)) return Task.CompletedTask;
        foreach (var property in type.Properties)
            if (property.Get is not null && property.ShouldSerialize is null && properties.ContainsKey(property.Name))
                (schema.Required ??= new HashSet<string>()).Add(property.Name);
        return Task.CompletedTask;
    }

    // Every type reachable from a JSON request body, including nested property and element types.
    private HashSet<Type> RequestTypes(JsonSerializerOptions options)
    {
        var found = new HashSet<Type>();
        var pending = new Stack<Type>(apis.ApiDescriptionGroups.Items.SelectMany(group => group.Items)
            .SelectMany(api => api.ParameterDescriptions).Where(parameter => parameter.Source == BindingSource.Body)
            .Select(parameter => parameter.Type));
        while (pending.TryPop(out var type))
        {
            if (!found.Add(Nullable.GetUnderlyingType(type) ?? type)) continue;
            var info = options.GetTypeInfo(Nullable.GetUnderlyingType(type) ?? type);
            foreach (var property in info.Properties) pending.Push(property.PropertyType);
            if (info.ElementType is { } element) pending.Push(element);
        }
        return found;
    }
}
