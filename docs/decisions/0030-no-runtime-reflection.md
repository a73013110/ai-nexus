# 0030 後端不在執行期反射，改為編譯期產生或強型別

- **狀態**：採用（2026-11-01）
- **決定**：
  - `backend/src` 不在執行期掃描組件、組泛型型別或以反射呼叫成員。各模組宣告、集中列出的清單由 source generator 在編譯期產生（`[FieldCatalog]`）；泛型在型別參數已知的地方建立：欄位宣告、DI 註冊、驗證器基底類別。
  - `backend/src/BannedSymbols.txt` 以 BannedApiAnalyzers（RS0030）擋下這些 API，每行寫明替代做法。確實無法避免時，在該成員加上寫明 `Justification` 的 `[SuppressMessage("ApiDesign", "RS0030")]`，與其他規則的例外寫法相同。測試專案不套用：架構測試以反射重述規則，對照產生的結果。
  - 套用到端點的驗證器只有 `RequestValidator<T>`：以 `IRequestValidator` 註冊，過濾器依 request 型別查表。同一型別有兩個驗證器時，建置（AINX1005）與啟動都會失敗。

## 原因

- **熱路徑成本**：表格排序每個請求都以名稱搜尋 `Queryable` 的方法再 `MakeGenericMethod`＋`Invoke`；監控取樣對每個 SQL 命令以 `GetProperty` 讀事件；診斷寫入逐格 `PropertyInfo.GetValue`。
- **悄悄失效**：驗證過濾器以 `IValidator<T>` 向 DI 查詢，同一型別有兩個驗證器時只取最後一個，另一個的規則沒有任何提示就不再執行。
- **型別參數其實已知**：表格欄位以 `Options<TValue>` 宣告、事件處理器以 `AddDomainEventHandler<TEvent, THandler>` 註冊、驗證器繼承 `RequestValidator<T>`。在那裡建立強型別物件，執行期只剩查表，也沒有「型別不能當泛型引數」這類只在執行期出現的錯誤。
- **掃描組件**：稽核動作清單每次啟動都掃描整個 Features。改由產生器列出後，看不到的欄位變成編譯錯誤，啟動不再付掃描成本；與 [ADR 0029](0029-compile-time-composition.md) 同一方向，也不妨礙之後做 trimming。
- **防線**：只寫在文件的規則會被遺忘；BannedSymbols 讓新程式在建置時失敗，訊息直接指向替代做法。

## 取捨

- 欄位目錄是通用的產生器，不是稽核專用：同樣「各模組宣告、集中列出」的需求不必再寫產生器。代價是規則寬鬆（static 類別裡型別相符的 public static 欄位都列入），所以元素型別要專用（如 `AuditAction`），不要用 `string` 這類通用型別。
- 只禁止會晚期繫結的 API：掃描組件、組泛型、`Invoke`、`GetValue`／`SetValue`、以字串找型別。`GetParameters`、`StackFrame.GetMethod`、組件屬性這類讀中繼資料的 API 不禁，框架介接仍需要。
- 運算式樹照常使用；EF 需要的 `MethodInfo` 以方法群組轉成委派取得，由編譯器解析多載。
- 領域事件的處理器要經 `AddDomainEventHandler` 註冊才有派送器；直接註冊 `IDomainEventHandler<T>` 收不到事件，由 `CompositionTests` 檢查。
- 監控取樣依賴 Microsoft.Data.SqlClient 6 的強型別診斷事件（`SqlClientCommandBefore` 等），換資料庫驅動時要改寫。

## 何時重新評估

- 需要在執行期載入事先不知道的組件（外掛）時。
