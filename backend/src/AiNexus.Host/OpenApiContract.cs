using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace AiNexus.Host;

/// <summary>
/// OpenAPI 文件規格合約產生器。
/// OpenAPI 文件為前端唯一的 API 型別來源（contracts/openapi.json → schema.ts），
/// 因此合約必須明確精確指出回應中各屬性是否必然存在（Required）。
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

        // [AsParameters] 類別會貢獻其 PascalCase 屬性名稱；模型繫結不分大小寫，而所有 API 屬性均為 camelCase，
        // 因此合約（與產生的客戶端）此處統一轉換為 camelCase。
        // 查詢陣列在未傳入時繫結為空陣列，框架常將其標記為必填，此處將其校正為選填（Required = false）。
        options.AddOperationTransformer((operation, _, _) =>
        {
            foreach (var parameter in operation.Parameters?.OfType<OpenApiParameter>() ?? [])
            {
                if (parameter.In != ParameterLocation.Query)
                {
                    continue;
                }

                if (parameter.Name is { } name)
                {
                    parameter.Name = JsonNamingPolicy.CamelCase.ConvertName(name);
                }

                if (parameter.Schema?.Type is { } type && type.HasFlag(JsonSchemaType.Array))
                {
                    parameter.Required = false;
                }
            }
            return Task.CompletedTask;
        });

        options.AddSchemaTransformer<ResponseSchemaRequirements>();
    });
}

/// <summary>
/// 回應 Schema 必填屬性轉換器。
/// 框架產生的結構僅將無預設值的建構子參數標記為必填，然而伺服器端會完整輸出回應物件之每個屬性（無全域跳過 null 行為）。
/// 因此針對僅作為輸出（Response）的型別，凡必然輸出的屬性皆標記為 Required；帶有條件式 JsonIgnore 的屬性維持選填。
/// 至於同時作為請求主體（Request Body）的型別則保留原樣，因呼叫端可省略帶預設值的欄位。
/// </summary>
internal sealed class ResponseSchemaRequirements(IApiDescriptionGroupCollectionProvider apis) : IOpenApiSchemaTransformer
{
    private HashSet<Type>? requestTypes;

    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var type = context.JsonTypeInfo;
        if (type.Kind != JsonTypeInfoKind.Object || schema.Properties is not { Count: > 0 } properties)
        {
            return Task.CompletedTask;
        }

        requestTypes ??= RequestTypes(type.Options);
        if (requestTypes.Contains(type.Type))
        {
            return Task.CompletedTask;
        }

        foreach (var property in type.Properties)
        {
            if (property.Get is not null && property.ShouldSerialize is null && properties.ContainsKey(property.Name))
            {
                (schema.Required ??= new HashSet<string>()).Add(property.Name);
            }
        }
        return Task.CompletedTask;
    }

    // 收集所有自 JSON 請求主體可達之型別（包含巢狀屬性與集合元素型別）。
    private HashSet<Type> RequestTypes(JsonSerializerOptions options)
    {
        var found = new HashSet<Type>();
        var pending = new Stack<Type>(apis.ApiDescriptionGroups.Items.SelectMany(group => group.Items)
            .SelectMany(api => api.ParameterDescriptions).Where(parameter => parameter.Source == BindingSource.Body)
            .Select(parameter => parameter.Type));

        while (pending.TryPop(out var type))
        {
            if (!found.Add(Nullable.GetUnderlyingType(type) ?? type))
            {
                continue;
            }

            var info = options.GetTypeInfo(Nullable.GetUnderlyingType(type) ?? type);
            foreach (var property in info.Properties)
            {
                pending.Push(property.PropertyType);
            }

            if (info.ElementType is { } element)
            {
                pending.Push(element);
            }
        }
        return found;
    }
}
