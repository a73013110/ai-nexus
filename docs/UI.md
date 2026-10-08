# 工作區互動與渲染

視覺、尺寸、字級與 motion 的維護規範見 [DESIGN_SYSTEM](DESIGN_SYSTEM.md)。

Chat、功能頁及閱讀器共用 `WorkspaceSidebar`／`WorkspaceLayout`。Toggle 在側欄頂部右側；桌面收合為 76px 圖示欄、手機為 64px，保留功能、通知與帳號。手機展開 overlay，背景 inert、Tab 焦點留在側欄、Escape 返回 toggle。桌面收合跨路由維持，進入手機時回到圖示欄。Active tabs 使用背景及內部下邊線，focus outline 內縮，避免被捲動容器裁切。

對話每列「…」與右上工具列共用 `ConversationActions`／top-layer ActionMenu，提供分享、命名、收藏、封存、刪除。生成中顯示 spinner，禁止破壞生成狀態的操作；切換後仍持續，完成圖示沿用未讀通知。準備回答採三層軌道與中心脈衝，減少動態時保持靜態可讀。

聊天附件卡片將閱讀、下載與移除分開，使用主題邊框、輕陰影及獨立的 hover／焦點回饋；手機維持可觸控尺寸。附件、回答引用、知識庫與專案文件共用 `ReaderLink`，在 URL 保存來源位置、同分頁的 history state 保存閱讀位置。閱讀頁依來源顯示「返回對話」「返回專案」或「返回知識庫」；返回聊天時保留對話、草稿、捲動位置與原附件焦點，也支援瀏覽器上一頁。重新整理或新分頁開啟仍保留來源 URL，只接受本站工作區路徑；未知來源顯示前往總覽，知識庫文件可回知識庫。閱讀頁共用文件面板、縮放／頁碼／檢視方式、原始圖片載入與錯誤回饋，遵循深色與減少動態設定。

聊天 lazy-loaded，Signal Forms、zoneless 與 OnPush。IME composition／229 Enter 不送出，Shift+Enter 換行；草稿自動增高，讀取歷史時保持捲動位置並提供「回到最新」。手機 drawer 有關閉、Escape、inert 與焦點返回。

串流增量透過共用 `FramePublisher` 合併為每 32ms 最多一次更新，terminal status 前立即 flush。`StreamingAnswer` 在畫面以約 30fps 平滑追上突發文字，只保留固定的兩個文字 span、避免半個 UTF-16 surrogate；完成／停止後改用伺服器完整 Markdown。等待狀態沿用軌道動態，準備回答採多層旋轉與中心脈衝，不顯示虛構思考內容。捲動跟隨每 frame 只排一次，使用者閱讀歷史時維持原行為。減少動態模式直接呈現最新文字。

登入頁延續工作區 token：桌面雙欄、手機單欄；自有傅立葉標誌動畫可跳過／重播，遵循減少動態，表單全程可用。提交後清除個人密碼，返回位置只接受列入白名單的本站功能／閱讀器路徑；登入／登出重設 ChatStore。SQL、AD 服務密碼與 Google key 在後端。

登入動畫先於中央描繪 N，完成後縮合至固定品牌錨點，最後依序顯示品牌文字、標題、說明與頁尾。畫布覆蓋品牌欄，不參與高度計算；常見桌面、375×667 與 320×568 手機保持一頁。低高度／大幅文字縮放仍允許必要的表單捲動以維持可操作性。

導覽、帳號選單、圖示、搜尋與勾選都由共用元件提供。設定視窗在 app 根層延遲載入，保留當前路由與內容；正常開關維持閱讀位置，實際變更字級時允許瀏覽器依新高度重排。標題與關閉按鈕固定在視窗頂部，捲動長設定清單仍可直接關閉。直接造訪舊 `/settings` 連結會於聊天背景開啟設定，維持書籤相容。管理員的使用者活動採原生 modal，對話保留寬版閱讀區、政策表單依內容收縮。稽核沿用系統日誌的共用 DataTable 與 DetailDrawer，桌面保留列表操作，窄螢幕轉為 modal；表單、細邊框與密度沿用相同共用元件。

對話輪次浮層以實際橫槓／輪次入口定位，限制在閱讀區及螢幕範圍；桌面在閱讀區右側中央，手機入口放在閱讀區右下，避開提問角色標籤。使用原生 popover top layer，Escape、點外部或離開預覽可關閉；動態文字以純文字摘要呈現。

Markdown 禁 raw HTML、external images、危險 URL；解析後經 DOMPurify tag／attribute allowlist，才進入 Angular trusted HTML boundary，以保留 code-copy button。不得把其他 HTML 傳到該 boundary。串流時先顯示純文字，完成／取消後渲染 Markdown，避免每 token 重算。

CSP 禁 inline script；build 關閉 inlineCritical 避免 CSS loader 的 inline onload 被擋。瀏覽器測試同時檢查互動、實際 grid／字級／尺寸、隱藏模型與 motion。artifacts/screenshots 的聊天畫面使用明確的測試使用者；正式執行不填入測試身分或回答。
