# AI Nexus 前端

Angular 22、TypeScript 6、Signals／Signal Forms、standalone、zoneless、OnPush、strict templates。聊天頁 lazy-loaded，API 型別由 OpenAPI 產生。

從 repository 根目錄執行 `./scripts/Start-Dev.ps1`，一次啟動 Angular 與 API，自動更新；整合預覽只執行 `./scripts/Start-Local.ps1`。完整指令見 [開發文件](../docs/development/DEVELOPMENT.md)。

features 管業務 UI／store，core 管 API／auth／stream／preferences，shared/ui 管可重用無業務狀態元件。`src/tokens.scss` 集中三層 token，設計規範见 [DESIGN_SYSTEM](../docs/frontend/DESIGN_SYSTEM.md)。

`src/app/core/api/schema.ts` 由 `npm run contracts`（或 Export-Contracts.ps1）從 `contracts/openapi.json` 產生，不手改；所有請求經過型別安全的 `ApiClient`，見 [前端共用邊界](../docs/frontend/FRONTEND_BOUNDARIES.md#呼叫-api)。Markdown 必須經 renderMarkdown 的解析與 DOMPurify allowlist；禁止 trust 任意 HTML。瀏覽器 fixture 只存在根目錄 tests/e2e，不打包進應用。
