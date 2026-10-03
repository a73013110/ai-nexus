# 工作台互動與渲染

視覺、尺寸、字級與 motion 的維護規範見 [DESIGN_SYSTEM](DESIGN_SYSTEM.md)。

聊天 lazy-loaded，Signal Forms、zoneless 與 OnPush。IME composition／229 Enter 不送出，Shift+Enter 換行；草稿自動增高，讀取歷史時保持捲動位置並提供「回到最新」。手機 drawer 有關閉、Escape、inert 與焦點返回。

登入頁延續工作台 token：桌面雙欄、手機單欄。提交後清除個人密碼，返回位置只接受本站聊天路徑；登入／登出重設 ChatStore。SQL、AD 服務密碼、Google key 全在後端。

Markdown 禁 raw HTML、external images、危險 URL；解析後經 DOMPurify tag／attribute allowlist，才進入 Angular trusted HTML boundary，以保留 code-copy button。不得把其他 HTML 傳到該 boundary。串流時先顯示純文字，完成／取消後渲染 Markdown，避免每 token 重算。

CSP 禁 inline script；build 關閉 inlineCritical 避免 CSS loader 的 inline onload 被擋。瀏覽器測試同時檢查互動、實際 grid／字級／尺寸、隱藏模型與 motion。artifacts/screenshots 的聊天畫面使用明確的測試使用者；正式執行不填入測試身分或回答。
