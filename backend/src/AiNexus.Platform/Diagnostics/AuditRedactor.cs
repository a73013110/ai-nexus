using System.Text.Json;

namespace AiNexus.Platform.Diagnostics;

/// <summary>
/// 稽核日誌的遮罩器。稽核紀錄有自己核准的結構描述與持久化交易策略，因此絕不抽樣略過。
/// </summary>
public static class AuditRedactor
{
    private static readonly HashSet<string> Fields = new(StringComparer.OrdinalIgnoreCase)
    {
        "resourceKey", "before", "after", "failureCode", "kind", "recipients", "expiresAt", "includeAttachments", "users", "groupIds", "source", "count", "externalId", "revision",
        "testId", "administratorId", "userId", "reason", "caseIndex", "variantIndex", "score", "id", "version", "conversationId", "offset", "profiles", "status", "activatedAt", "retiredAt", "vectors",
        "allowedModelIds", "dailyTokenLimits", "storedAttachmentLimitBytes", "attachmentLimitBytes", "enabled", "account", "displayName", "deletedAt", "securityVersion", "authentication",
        "roleIds", "name", "policy", "featureIds", "sortOrder", "adAccount", "adEnabled", "localAccount", "localEnabled", "hasLocalPassword", "from", "to", "issueCode", "traceId", "jobId", "runId", "permission", "clientAddress",
        "fingerprint", "retentionDays", "auditRetentionDays", "fileRetentionDays", "queueCapacity", "importantQueueCapacity", "batchSize", "maxDiskBytes", "maxSqlRows", "minimumLevel", "frameworkMinimumLevel", "lowLevelSampleEvery", "otlpEnabled",
        "fileSizeBytes", "flushIntervalMs", "retrySeconds", "sqlTimeoutSeconds", "shutdownSeconds", "maxQueryDays", "maxExportDays", "maxExportRows", "serviceName", "cleanupBatchSize", "locationFingerprint",
        "search", "action", "results", "category", "provider", "modelId", "effectiveAt", "parentId", "featureId", "openInNewTab", "icon", "audienceGroupIds", "unitId", "unitIds", "memberships", "isPrimary", "isManager",
        "scopes", "scope", "attempts", "nextAttemptAt", "autoRetry", "transport", "failedStep", "certificateVerdict",
        // 出勤卡號對應（ADR 0018）：卡號、持有期間與還沒有帳號的持有人姓名；備註是自由文字，不收。
        "cardId", "cardNumber", "assignments", "startsAt", "endsAt", "holderName",
        // 出勤假日設定回寫 GD 的前後值：上午／下午放假、備註（節日名稱，例如「國慶日」，不是個人資料）與 GD 的修改者帳號。
        "morningOff", "afternoonOff", "calendarRemark", "gdUserId",
        // 組織從公文系統匯入（admin.organization_import）：前後的單位、已匯入單位與主管數，以及這次各類變動的數量；不收部門或人員清單。
        "units", "gdUnits", "managers", "created", "adopted", "updated", "disabled", "managersAdded", "managersRemoved", "issues",
        // 出勤紀錄匯出的查詢條件（區間、人員、單位用既有欄位；人數用 count）。
        "includeExcluded"
    };

    /// <summary>
    /// 清理稽核 JSON：只保留核准的白名單欄位，並限制巢狀深度與大小。
    /// </summary>
    public static string? Sanitize(string? json, bool historical = false)
    {
        if (json is null)
        {
            return null;
        }

        try
        {
            if (json.Length > 40000)
            {
                throw new JsonException();
            }

            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 8 });
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                Write(writer, document.RootElement, "", 0);
            }

            var result = System.Text.Encoding.UTF8.GetString(stream.ToArray());
            if (result.Length > 40000)
            {
                throw new JsonException();
            }

            return result;
        }
        catch (JsonException) when (historical)
        {
            return "{\"diagnostic\":\"[LEGACY DETAIL OMITTED]\"}";
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("稽核紀錄無法安全保存。");
        }
    }

    private static void Write(Utf8JsonWriter writer, JsonElement value, string key, int depth)
    {
        if (depth > 7)
        {
            throw new JsonException();
        }

        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                if (value.EnumerateObject().Count() > 128)
                {
                    throw new JsonException();
                }

                writer.WriteStartObject();
                foreach (var property in value.EnumerateObject())
                {
                    var dictionaryKey = key.Equals("dailyTokenLimits", StringComparison.OrdinalIgnoreCase)
                        && property.Name.Length <= 160
                        && property.Name.All(c => char.IsAsciiLetterOrDigit(c) || c is '/' or ':' or '-' or '_' or '.');

                    if (!Fields.Contains(property.Name) && !dictionaryKey)
                    {
                        continue;
                    }

                    writer.WritePropertyName(property.Name);
                    Write(writer, property.Value, property.Name, depth + 1);
                }
                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                if (value.GetArrayLength() > 128)
                {
                    throw new JsonException();
                }

                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray())
                {
                    Write(writer, item, key, depth + 1);
                }
                writer.WriteEndArray();
                break;

            case JsonValueKind.String:
                var text = value.GetString();
                // 自由填寫的身分測試理由可能含有機敏文件或對話文字，因此只保留「有填寫」的事實。
                // 伺服器預設的固定工作階段結束或撤銷代碼則保留原值。
                if (key.Equals("reason", StringComparison.OrdinalIgnoreCase) && text is not ("returned" or "signed_in" or "signed_out" or "expired" or "target_changed"))
                {
                    text = "[REASON PROVIDED; TEXT OMITTED]";
                }
                writer.WriteStringValue(DiagnosticRedactor.Text(text, 240));
                break;

            case JsonValueKind.Number:
                value.WriteTo(writer);
                break;

            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;

            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;

            default:
                writer.WriteNullValue();
                break;
        }
    }
}
