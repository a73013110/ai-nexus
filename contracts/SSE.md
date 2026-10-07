# AI Nexus 串流契約 v1

`POST /api/v1/runs` 建立背景工作，回傳 202 與 RunDto；必須提供 `Idempotency-Key`（16–80 個 ASCII 英數／連字號／底線字元）與 `X-Nexus-CSRF`。同一使用者、同一鍵、同一內容回傳同一 run；同鍵不同內容回傳 409。查核冪等紀錄先於 provider 探查，所以斷線重送不依賴模型當下可用性。

`GET /api/v1/runs/{id}/events?after={sequence}` 使用 AD cookie 或 Windows 驗證訂閱 SSE；`Last-Event-ID` 優先於 query cursor。每個訂閱必須屬於該 run 的擁有者。讀取、取消與對話路由皆再次確認擁有者及未刪除狀態。

```text
id: 3
event: run
data: {"version":1,"sequence":3,"runId":"...","type":"delta","status":"running","delta":"你好","errorCode":null}

```

序號從 1 遞增，在資料庫保存後才可送出。`type` 為 `status`、`delta`、`snapshot`。`snapshot.delta` 是完整的最新內容，須取代本機內容；`delta.delta` 才是附加文字。未知事件版本／種類可忽略。狀態为 `queued → running → completed | cancelled | failed`，排隊也可直接取消。

失敗的RunDto／MessageDto及run status／snapshot新增nullable `issueCode`，格式為伺服器產生的 `NX-`＋32個hex字元；保留errorCode供重試分類。前端以固定安全提示與複製按鈕顯示，禁止使用provider原始error message。若SSE已開始而訂閱處理中途失敗，邊界送 `event: error`，data只有安全的`code`、`message`與`issueCode`；前端將它解析為安全ApiError。沒有有效伺服器碼的網路錯誤顯示明確LOCAL代碼。詳見 [錯誤與關聯規範](../docs/DIAGNOSTICS.md#安全錯誤契約與流程關聯)。

回應型別 `text/event-stream`，UTF-8、LF framing、每 10 秒 heartbeat comment、禁用 buffering／快取。每個 SSE write 最多等待 5 秒。每個使用者最多 2 條訂閱，全域最多 64 條。斷線只停止訂閱，背景生成繼續；停止生成須呼叫 `POST /runs/{id}/cancel`。

事件短期保留 24 小時。cursor 過期或保留範圍有缺口時提供 snapshot；cursor 負值或超前回傳 409。終止狀態送完事件後關閉連線。

前端以 GET RunDto 取得完整內容與 `lastSequence` 後恢復訂閱，依序號去重。重連不能重送 POST。POST 回應遺失時，保留原提交鍵與 payload 供重試；重新整理後以 me.activeRunId／conversation.activeRun 恢復。取消立即在 UI 回饋，已保存的部分回答保留。

單一程序、單一生成 worker；IIS 不可使用 web garden。GenerationRuns保存ExecutorId與租約，啟動及定期只回收逾期executor的active run，避免其他活躍實例被誤中止。回收失敗也保存查證碼、Trace／Run關聯與安全通知。日誌本身支援多實例補送，並不表示聊天排程已提供全域容量或durable queue。

所有聊天／模型／Context／run endpoints 需要 `feature:chat` 授權，並保留原 owner 驗證。`/me` 回傳有效角色、群組與 features。每個新 request 重查有效 grant；既有串流連線不會因資料庫授權修改自動中斷。

`/models` 回傳 `policy`（allowModelSelection／showModelNames／defaultModelId）與各模型 reasoningEfforts／defaultReasoningEffort。`POST /runs` 的 modelId 可為 null，由系統預設；reasoningEffort 選用，省略則採 profile 預設。鎖定時拒絕其他模型，未支援思考值回 400。隱藏名稱時所有模型 DTO 使用 public alias，客戶端不可假設 ID 等於 provider ID。

`POST /context` 需 CSRF，接受 conversationId／parentMessageId／prompt／modelId，驗 owner 後回傳 estimatedInputTokens、contextTokens、reservedOutputTokens、droppedMessages、budgetExceeded 與 isEstimate。parentMessageId=null 表示根提問；前端續問須傳目前 leaf，編輯根訊息仍傳 null。預覽不呼叫模型、不寫入歷史；超限仍由實際 run 建立交易再次驗證。
