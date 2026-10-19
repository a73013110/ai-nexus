using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.AccessControl.Scopes;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace AiNexus.Host;

/// <summary>
/// OpenAPI 文件規格合約產生器。
/// 前端的 API 型別只從這份 OpenAPI 文件產生（contracts/openapi.json → schema.ts），
/// 因此必須精確標出回應屬性是否必然存在（Required）。
/// </summary>
internal static class OpenApiContract
{
    public static void AddOpenApiContract(this IServiceCollection services) => services.AddOpenApi(options =>
    {
        options.AddDocumentTransformer((document, _, _) =>
        {
            document.Servers = [new() { Url = "/" }];
            // 沒有端點直接引用：功能 id 在 DTO 裡仍是字串（資料庫可能有程式不認得的列），這份列舉只給前端產生 FeatureId 型別。
            // 只列授權（頁面＋動作）；閱讀器不能授予，放進列舉會讓前端以為能檢查它。
            document.Components ??= new();
            document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>();
            document.Components.Schemas["FeatureId"] = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Enum = [.. FeatureCatalog.Grants.Select(feature => (JsonNode)JsonValue.Create(feature.Id))],
            };
            document.Extensions ??= new Dictionary<string, IOpenApiExtension>();
            document.Extensions["x-feature-catalog"] = new JsonNodeExtension(FeatureCatalogJson());
            return Task.CompletedTask;
        });

        // [AsParameters] 類別的屬性名稱是 PascalCase，但所有 API 參數都是 camelCase。
        // 這裡統一改成 camelCase，合約與產生的客戶端才會一致。
        // 查詢陣列未傳入時會繫結為空陣列，框架卻常標成必填，這裡校正為選填。
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

    /// <summary>
    /// 整份頁面目錄（區、模組、功能），前端由此產生路由對照、麵包屑與名稱；順序同目錄宣告，前端產生物因此穩定。
    /// </summary>
    private static JsonObject FeatureCatalogJson() => new()
    {
        ["areas"] = new JsonArray([.. FeatureCatalog.Areas.Select(area => (JsonNode)new JsonObject { ["id"] = area.Id, ["name"] = area.Name })]),
        ["modules"] = new JsonArray([.. FeatureCatalog.Modules.Select(module => (JsonNode)new JsonObject
        {
            ["id"] = module.Id, ["area"] = module.Area.Id, ["name"] = module.Name,
        })]),
        ["features"] = new JsonArray([.. FeatureCatalog.All.Select(feature => (JsonNode)new JsonObject
        {
            ["id"] = feature.Id,
            ["kind"] = feature.Kind switch { FeatureKind.Page => "page", FeatureKind.Action => "action", _ => "reader" },
            ["module"] = feature.Module.Id,
            ["name"] = feature.Name,
            ["icon"] = feature.Icon,
            ["route"] = feature.Route,
            ["scopes"] = new JsonArray([.. feature.Scopes.Select(scope => (JsonNode)JsonValue.Create(scope))]),
        })]),
        ["scopes"] = new JsonArray([.. DataScopes.Definitions.Select(scope => (JsonNode)new JsonObject { ["id"] = scope.Id, ["name"] = scope.Name })]),
    };
}

/// <summary>
/// 回應 Schema 必填屬性轉換器。
/// 框架只把沒有預設值的建構子參數標成必填，但伺服器會輸出回應物件的每個屬性（沒有全域略過 null）。
/// 因此只作為回應的型別，會輸出的屬性一律標為 Required；帶條件式 JsonIgnore 的屬性維持選填。
/// 同時當作請求主體的型別保持原樣，因為呼叫端可以省略有預設值的欄位。
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

    // 收集所有能從 JSON 請求主體走到的型別，包含巢狀屬性與集合元素。
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
