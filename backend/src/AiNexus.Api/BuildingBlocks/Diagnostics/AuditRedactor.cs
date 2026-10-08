using System.Text.Json;

namespace AiNexus.BuildingBlocks.Diagnostics;

/// <summary>Audit has its own approved schema and durable transaction policy; it is never sampled.</summary>
public static class AuditRedactor
{
    private static readonly HashSet<string> Fields = new(StringComparer.OrdinalIgnoreCase) {
        "resourceKey", "before", "after", "failureCode", "kind", "recipients", "expiresAt", "includeAttachments", "users", "groupIds", "source", "count", "externalId", "revision",
        "testId", "administratorId", "userId", "reason", "caseIndex", "variantIndex", "score", "id", "version", "conversationId", "offset", "profiles", "status", "activatedAt", "retiredAt", "vectors",
        "allowedModelIds", "dailyTokenLimits", "storedAttachmentLimitBytes", "attachmentLimitBytes", "enabled", "account", "displayName", "deletedAt", "securityVersion", "authentication",
        "roleIds", "name", "policy", "featureIds", "sortOrder", "adAccount", "adEnabled", "localAccount", "localEnabled", "hasLocalPassword", "from", "to", "issueCode", "traceId", "jobId", "runId", "permission", "clientAddress",
        "fingerprint", "retentionDays", "auditRetentionDays", "fileRetentionDays", "queueCapacity", "importantQueueCapacity", "batchSize", "maxDiskBytes", "maxSqlRows", "minimumLevel", "frameworkMinimumLevel", "lowLevelSampleEvery", "otlpEnabled",
        "fileSizeBytes", "flushIntervalMs", "retrySeconds", "sqlTimeoutSeconds", "shutdownSeconds", "maxQueryDays", "maxExportDays", "maxExportRows", "serviceName", "cleanupBatchSize", "locationFingerprint"
    };
    public static string? Sanitize(string? json, bool historical = false)
    {
        if (json is null) return null;
        try
        {
            if (json.Length > 40000) throw new JsonException();
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 8 });
            using var stream = new MemoryStream(); using (var writer = new Utf8JsonWriter(stream)) Write(writer, document.RootElement, "", 0);
            var result = System.Text.Encoding.UTF8.GetString(stream.ToArray());
            if (result.Length > 40000) throw new JsonException();
            return result;
        }
        catch (JsonException) when (historical) { return "{\"diagnostic\":\"[LEGACY DETAIL OMITTED]\"}"; }
        catch (JsonException) { throw new ApiException(503, "audit_unavailable", "稽核紀錄無法安全保存。"); }
    }
    private static void Write(Utf8JsonWriter writer, JsonElement value, string key, int depth)
    {
        if (depth > 7) throw new JsonException();
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                if (value.EnumerateObject().Count() > 128) throw new JsonException();
                writer.WriteStartObject();
                foreach (var property in value.EnumerateObject())
                {
                    var dictionaryKey = key.Equals("dailyTokenLimits", StringComparison.OrdinalIgnoreCase) && property.Name.Length <= 160 && property.Name.All(c => char.IsAsciiLetterOrDigit(c) || c is '/' or ':' or '-' or '_' or '.');
                    if (!Fields.Contains(property.Name) && !dictionaryKey) continue;
                    writer.WritePropertyName(property.Name); Write(writer, property.Value, property.Name, depth + 1);
                }
                writer.WriteEndObject(); break;
            case JsonValueKind.Array:
                if (value.GetArrayLength() > 128) throw new JsonException();
                writer.WriteStartArray(); foreach (var item in value.EnumerateArray()) Write(writer, item, key, depth + 1); writer.WriteEndArray(); break;
            case JsonValueKind.String:
                var text = value.GetString();
                // Free-form identity-test justifications may contain document/chat content. Retain their presence,
                // while preserving the server's fixed reasons for session end/revocation.
                if (key.Equals("reason", StringComparison.OrdinalIgnoreCase) && text is not ("returned" or "signed_in" or "signed_out" or "expired" or "target_changed")) text = "[REASON PROVIDED; TEXT OMITTED]";
                writer.WriteStringValue(DiagnosticRedactor.Text(text, 240)); break;
            case JsonValueKind.Number: value.WriteTo(writer); break;
            case JsonValueKind.True: writer.WriteBooleanValue(true); break;
            case JsonValueKind.False: writer.WriteBooleanValue(false); break;
            default: writer.WriteNullValue(); break;
        }
    }
}
