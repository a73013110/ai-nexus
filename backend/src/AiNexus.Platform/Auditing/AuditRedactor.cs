using System.Text.Json;
using AiNexus.Platform.Diagnostics;

namespace AiNexus.Platform.Auditing;

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
        "roleIds", "name", "policy", "featureIds", "sortOrder", "adAccount", "adEnabled", "localAccount", "localEnabled", "hasLocalPassword", "from", "to", "issueCode", "traceId", "jobId", "runId", "permission",
        "fingerprint", "retentionDays", "auditRetentionDays", "fileRetentionDays", "queueCapacity", "importantQueueCapacity", "batchSize", "maxDiskBytes", "maxSqlRows", "minimumLevel", "frameworkMinimumLevel", "lowLevelSampleEvery", "otlpEnabled",
        "fileSizeBytes", "flushIntervalMs", "retrySeconds", "sqlTimeoutSeconds", "shutdownSeconds", "maxQueryDays", "maxExportDays", "maxExportRows", "serviceName", "cleanupBatchSize", "locationFingerprint",
        "search", "action", "results", "category", "provider", "modelId", "effectiveAt", "parentId", "featureId", "openInNewTab", "icon", "audienceGroupIds", "unitId", "unitIds", "memberships", "isPrimary", "isManager",
        "scopes", "scope", "managerScope", "attempts", "nextAttemptAt", "autoRetry", "transport", "failedStep", "certificateVerdict",
        // 出勤卡號對應（ADR 0018）：卡號、持有期間與還沒有帳號的持有人姓名；備註是自由文字，不收。
        "cardId", "cardNumber", "assignments", "startsAt", "endsAt", "holderName",
        // 出勤假日設定回寫 GD 的前後值：上午／下午放假、備註（節日名稱，例如「國慶日」，不是個人資料）與 GD 的修改者帳號。
        "morningOff", "afternoonOff", "calendarRemark", "gdUserId",
        // 組織從公文系統匯入（organization.unit.imported）：前後的單位、已匯入單位與主管數，這次排除的部門數與各類變動的數量；不收部門或人員清單。
        "units", "gdUnits", "managers", "excluded", "created", "adopted", "updated", "disabled", "managersAdded", "managersRemoved", "issues",
        // 首長單位（ADR 0024）：層級，與設定時被取消主管勾選的上層主管（使用者 ID）。
        "headRank", "parentManagerIds",
        // 出勤紀錄匯出的查詢條件（區間、人員、單位與卡號用既有欄位；人數用 count）；卡片快照的隱藏旗標。
        "includeDaysOff", "hiddenFromRecords",
        // 外部系統（identity.api_client*）：名稱用既有的 name；金鑰只記末四碼與產生時間，雜湊與明碼都不收。
        // client 是帳密驗證（identity.credentials.verified）時呼叫的系統名稱，由管理者命名、不是個人資料。
        "keyHint", "keyIssuedAt", "client",
        // 系統動作（背景工作）的標記與向量索引設定；管理者開啟他人對話附件時的檔案 ID；分享對象的群組數。
        "automatic", "profileId", "fileId", "groups",
        // 排程匯入的新刷卡數用 count、自動建立的卡片數 cards；手動重匯找不到的檔數 missing。
        "cards", "missing",
        // 郵件保存期清理（mail.message.cleared）：逾期未寄、刪內文、刪紀錄的筆數；清掉收件人的筆數用既有的 recipients。
        "expired", "contents", "records",
        // 日曆同步（calendar_sync.*）：一輪的建立、更新數用既有的 created、updated，刪除與失敗數 deleted、failed；手動同步是否確認被安全閥擋下的移除。
        "deleted", "failed", "confirmRemovals",
        // 請求稽核自動記下的路由參數與查詢字串（AuditTrail.RequestDetails）：鍵是參數名稱，值已在寫入前換成安全的短字串。
        "route", "query"
    };

    /// <summary>明細鍵是否在白名單內；不在的鍵寫入時會被丟掉，<c>AuditDetailTests</c> 用它檢查宣告的明細。</summary>
    public static bool Allows(string key) => Fields.Contains(key);

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
                    // 這幾個物件是字典：鍵是模型 ID 或參數名稱，不在白名單裡，改以字元限制把關。
                    var dictionaryKey = (key.Equals("dailyTokenLimits", StringComparison.OrdinalIgnoreCase) || key is "route" or "query")
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
