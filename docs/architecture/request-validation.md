# 請求驗證

request body 的格式規則、欄位規則代碼與文字規則。slice 其餘寫法見 [後端撰寫慣例](backend-conventions.md)。

## 驗證器

- request body 的格式規則寫成 `RequestValidator<T>`，與 request 同檔；`/api/v1` 群組的 filter 在 handler 前驗證。單純的 `AbstractValidator<T>` 只當子驗證器；同一型別只能有一個（AINX1005）。
- 失敗預設回 `validation_failed`；有專屬提示時覆寫 `Problem`，回傳 `<Module>Errors` 的 `Error.Invalid`（`EndpointConventionTests`）。
- validator 是 singleton、只看 request：建構子只注入 singleton（options、`TimeProvider`）；依賴使用者或資料庫的規則（重複、配額、擁有權）在 handler。
- 刻意交給 handler 驗證的 request（失敗要寫稽核、登入失敗不可分辨、404／403 先於 400）不寫 validator，改標 `[ValidatedInHandler("原因")]`；兩者擇一（`EndpointConventionTests`）。

## 欄位規則代碼

- 代碼是公開合約，只有 `RuleCode` 一套詞彙。內建規則不必指定，回應時自動對應（`NotEmpty` → `required`、`MaximumLength` → `too_long`）；沒指定的 `Must` 是 `invalid`，不外露 FluentValidation 的類別名稱。
- 指定代碼用 `WithErrorCode(RuleCode.X)`，字串版禁用（BannedSymbols）。只有一個功能用到的宣告在驗證器（`new("x")`），第二個模組要用時再搬進 `RuleCode`。

## 文字

規則只有一份（`TextRules`），驗證器、實體與 handler 共用；長度以去掉前後空白後計算。

| 欄位 | 驗證器 | 實體、handler | 控制字元 |
|---|---|---|---|
| 單行：名稱、標題、標籤、路徑 | `RequiredText`／`OptionalText` | `IsLine`、`HasLineControl` | 全部拒絕 |
| 可換行：內文、說明、備註、留言 | `RequiredContent`／`OptionalContent` | `IsContent`、`HasContentControl` | 只允許換行與 Tab |

- 失敗回 `required`／`too_long`／`format`。
- 不套用：識別碼、密碼與驗證碼（只限長度，用 `MaximumLength`）、聊天提問（原樣交給模型，只限長度）、email 與網址（有自己的字元規則）。
