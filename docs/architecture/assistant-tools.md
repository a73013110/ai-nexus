# 助理工具

結論：一個功能要接上 [助理](assistant.md)，只要在自己的模組寫 [`IAssistantTool`](../../backend/src/AiNexus.Features/Platform/Assistant/IAssistantTool.cs)。工具負責查資料、算數字、判斷設定有沒有問題，寫成一條條事實。模型只負責轉述。「為什麼出現、為什麼不見」這類問題不逐案寫回答規則：交出完整的設定與條文，模型自己組合說明。範例是 [出勤](../features/attendance-assistant.md)。

## 成員

- `Name`：模型看到的工具名，snake_case、全域唯一、以模組開頭，例 `attendance_records`。
- `Title`：查詢時島上與面板顯示的進度，例「查詢判定過程」。
- `Description`：寫給模型看：查得到什麼、什麼時候用、參數不填代表什麼。
- `Feature`：持有這個功能才拿得到工具，資料範圍也跟著它。工具內部呼叫該頁的查詢 handler，範圍外的資料不會進到模型。
- `Guidance`：這個領域的名詞與「該用哪個工具」，寫進系統指令。判定條件不寫在這裡，寫成〈規則〉事實。
- `Parameters`：用 [`AssistantParameters`](../../backend/src/AiNexus.Features/Platform/Assistant/AssistantParameters.cs) 宣告日期、文字、選項。它同時產生 JSON Schema，並驗證模型給的參數。
- `Audit`：每次呼叫寫的稽核動作。只讀設定的工具給 null。
- `DescribesPage`：是否描述 `Feature` 那一頁的畫面。是的話，在那一頁提問時會先用畫面條件執行一次。條件用 `call.ReadView<查詢參數型別>(JsonOptions)` 讀，型別不對就當成條件不合法。

## 事實

回傳 `AssistantToolResult(Scope, Facts)`。事實依重要性排序，放不下時從後面捨去。數字與結論要來自既有的判定，與畫面同一份。

| 種類 | 寫法 | 放什麼 |
|---|---|---|
| 資料 | `new AssistantFact(label, text)` | 查到的資料，以及對象的設定（帳號狀態、持有期間、是否隱藏） |
| 〈規則〉 | `AssistantFact.Rule(title, text)` | 判定條件、資料怎麼列出（誰會出現、什麼情況不列）。條文放在實作條件的類別旁邊，一起改 |
| 〈檢查〉 | `AssistantFact.Check(label, text, link)` | 系統比對設定後發現的問題、影響與處理方式。`AssistantLink(功能 ID, 查詢字串)` 指向處理頁 |

- 平台會自動補上的，工具不用自己寫：
  - 提問者在這個功能的資料範圍。是〈規則〉「你的資料範圍」，每個功能補一次。
  - 系統指令裡〈規則〉〈檢查〉的用法。
  - 處理頁的名稱與網址。只有提問者持有那一頁才給網址。
- 〈檢查〉只寫真的有問題的情況。刻意的設定（例如隱藏的卡）寫成資料，由〈規則〉說明它的效果。
- 已經有帳號待辦（`IAccountReviewSource`）或監控來源判斷同一件事時，〈檢查〉沿用同一個條件。
- 預期內的失敗回 `Result` 錯誤，不 throw。查不到人不是失敗：回傳結果，並說明查詢範圍。

## 工具的分工

一個功能通常需要以下幾種工具，名稱前綴相同：

1. **頁面資料**（`DescribesPage`）：畫面條件下的整份結果。附上名單裡的人的〈檢查〉，常見問題一輪就能回答。
2. **單筆說明**：一筆資料為什麼是這個結果，例如判定過程、失敗原因、狀態歷程。
3. **對象設定**：某個人或某筆資料為什麼會出現或不出現。內容是完整設定、已知問題與列出的〈規則〉。資料範圍與頁面相同，但不套用頁面為了好看而略過的條件（例如隱藏）。
4. **規則**：條文本身，不含個人資料，不寫稽核。

## 新增一個功能的助理

1. 後端：在模組的 `Assistant/` 寫工具。事實的組法寫成純函式並寫單元測試，再把功能加進 `Policies.Models`。
2. 整合測試（參考 `AskAssistantTests`）：
   - 範圍外的人不會進到模型。
   - 不合法的條件在模型呼叫之前就被拒絕。
   - 工具結果的編號接續，並寫了稽核。
   - 〈檢查〉的連結只給有處理頁權限的人。
3. 前端：頁面呼叫 `inject(AssistantPages).provide({...})`，不需要自己的 API 方法或面板。
4. 文件：在 `docs/features/` 寫一份該功能的助理，列出工具與事實的順序。各功能可以先做哪些見 [助理候選用途](../research/assistant-use-cases.md)。
