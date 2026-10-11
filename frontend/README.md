# AI Nexus 前端

Angular 22、TypeScript 6、standalone、zoneless、OnPush、strict templates；頁面依路由 lazy-load，狀態用 Signals，表單以 Signal Forms 為主。

## 指令

從 repository 根目錄執行 `./scripts/Start-Dev.ps1`，一次啟動 Angular 與 API，自動更新；整合預覽執行 `./scripts/Start-Local.ps1`。完整流程見 [開發文件](../docs/development/development.md)。這個資料夾是唯一的 npm 根目錄：

| 指令 | 用途 |
| --- | --- |
| `npm run lint` | ESLint（含資料夾依賴方向與 `ApiClient` 使用限制） |
| `npm test` | 單元測試（Vitest，`src/**/*.spec.ts`） |
| `npm run build` | 正式建置到 `dist/` |
| `npm run contracts` | 由 `../contracts/openapi.json` 重產 `src/app/core/api/schema.ts`；`contracts:check` 只檢查是否一致 |
| `npm run test:e2e` | 對發布產物跑 `e2e/` 的 Playwright 測試，見 [測試](../docs/development/testing.md#瀏覽器測試) |

## 規則在哪裡

- 資料夾分工、依賴方向、呼叫 API 與讀寫寫法：[前端共用邊界](../docs/frontend/frontend-boundaries.md)。
- `schema.ts` 是產生物，不手改；請求一律經過型別安全的 `ApiClient`，型別直接用後端 DTO 名稱。
- 介面元件與互動：[共用介面模式](../docs/frontend/ui-patterns.md)；token、主題與樣式載入：[設計系統](../docs/frontend/design-system.md)。
- Markdown 一律經 `MarkdownView` 的解析與 DOMPurify allowlist，不得 trust 任意 HTML：[聊天渲染](../docs/frontend/chat-rendering.md)。
- 測試替身只在 `e2e/` 與 `*.spec.ts`，不打包進應用。
