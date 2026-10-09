# 工作區互動與渲染

視覺、尺寸、字級與 motion 的維護規範見 [DESIGN_SYSTEM](DESIGN_SYSTEM.md)。

Chat、功能頁及閱讀器共用 `WorkspaceSidebar`／`WorkspaceLayout`。Toggle 在側欄頂部右側；桌面收合為 52px 圖示欄，保留功能、通知與帳號。手機收合時主內容使用完整寬度，展開為 overlay，背景 inert、Tab 焦點留在側欄、Escape 返回 toggle；桌面收合狀態跨路由及手機斷點維持。功能導覽使用共用 `ScrollArea`，隱藏捲軸且以實際位置控制漸層箭頭，保留方向按鈕、鍵盤、觸控、高對比與減少動態效果。Active tabs 使用背景及內部下邊線，focus outline 內縮，避免被捲動容器裁切。詳見 [共用介面模式](UI_PATTERNS.md)。

對話每列「…」與右上工具列共用 `ConversationActions`／top-layer ActionMenu，提供分享、命名、收藏、封存、刪除。生成中顯示 spinner，禁止破壞生成狀態的操作；切換後仍持續，完成圖示沿用未讀通知。準備回答採三層軌道與中心脈衝，減少動態時保持靜態可讀。

側欄的對話操作、搜尋、篩選、歷史清單、工作區開關與帳號選單沿用 `ui-density-compact`；品牌區與工作區功能分組保留既有尺度。側欄以 `--density-text-size`／`--density-caption-size` 使用 14px 主文字、13px 輔助文字，與功能導覽的 14px 尺度一致；桌面列高 34px、歷史列間距 2px、搜尋列 40px，側欄 overlay／觸控維持 44px。品牌下方保留 12px 間距，其他外框間距 4px，移除工作區按鈕與帳號之間的冗餘留白。帳號使用 28px 頭像及兩行省略文字，共用 `ActionMenu` 樣式集中於 `styles/action-menu.scss`，選單移除列間空隙但保留完整鍵盤／焦點操作。聊天操作區亦共用緊湊尺度，正文仍使用個人閱讀設定；48px 頁首、32px 最小輸入高度與 16px／12px 訊息間距減少冗餘留白，長草稿依內容自動增高。新對話的標題、輸入與建議仍在畫面中央，已有訊息時輸入區固定於底部。

密度與一致間距參考 [Fluent 2 layout](https://fluent2.microsoft.design/layout)；桌面操作尺寸遵循 [WCAG 2.2 target size](https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum)，手機維持較大的觸控範圍。

設定中的「內容寬度」同步控制訊息、開場、輸入框、附件預覽、狀態與下方提示，透過同一個 `--reading-width` 與 `.chat-column` 配置，不再維持另一份固定輸入寬度。共用 `.chat-gutter` 同時處理留白與捲軸保留空間，避免寬廣模式或小視窗出現邊緣不對齊。設定預覽、取消、儲存與重新載入沿用同一偏好流程；手機以可用寬度為上限。共用附件列表以 container query 適應實際欄寬，窄容器採 32px 縮圖及精簡間距，檔名與資訊保持單行並省略，完整檔名保留在可見工具提示與閱讀／下載／移除的名稱中，觸控操作仍為 44px。展開按鈕定位在文字輸入列，避免與附件操作重疊；放大的長文輸入視窗也沿用選定欄寬，保留同步草稿與較大的編輯高度。「回到最新」屬於閱讀區浮動操作，不受輸入區邊界裁切。

聊天附件卡片將閱讀、下載與移除分開，使用主題邊框、輕陰影及獨立的 hover／焦點回饋；手機維持可觸控尺寸。附件、回答引用、知識庫與專案文件共用 `ReaderLink`，在 URL 保存來源位置、同分頁的 history state 保存閱讀位置。閱讀頁依來源顯示「返回對話」「返回專案」或「返回知識庫」；返回聊天時保留對話、草稿、捲動位置與原附件焦點，也支援瀏覽器上一頁。重新整理或新分頁開啟仍保留來源 URL，只接受本站工作區路徑；未知來源顯示前往總覽，知識庫文件可回知識庫。閱讀頁共用文件面板、縮放／頁碼／檢視方式、原始圖片載入與錯誤回饋，遵循深色與減少動態設定。

聊天 lazy-loaded，Signal Forms、zoneless 與 OnPush。IME composition／229 Enter 不送出，Shift+Enter 換行；草稿自動增高，讀取歷史時保持捲動位置並提供「回到最新」。手機 drawer 有關閉、Escape、inert 與焦點返回。

串流增量透過共用 `FramePublisher` 合併為每 32ms 最多一次更新，terminal status 前立即 flush。`StreamingAnswer` 以約 20fps 平滑追上突發文字，避免半個 UTF-16 surrogate；Markdown 如何分段重算見 [聊天渲染](CHAT_RENDERING.md)；完成／停止後改用伺服器完整 Markdown。等待狀態沿用軌道動態，準備回答採多層旋轉與中心脈衝，不顯示虛構思考內容。捲動跟隨每 frame 只排一次，使用者閱讀歷史時維持原行為。減少動態模式直接呈現最新文字。

登入頁延續工作區 token：桌面雙欄、手機單欄；自有傅立葉標誌動畫可跳過／重播，遵循減少動態，表單全程可用。提交後清除個人密碼，返回位置只接受列入白名單的本站功能／閱讀器路徑；登入／登出重設 ChatStore。SQL、AD 服務密碼與 Google key 在後端。

登入動畫先於中央描繪 N，完成後縮合至固定品牌錨點，最後依序顯示品牌文字、標題、說明與頁尾。畫布覆蓋品牌欄，不參與高度計算；常見桌面、375×667 與 320×568 手機保持一頁。低高度／大幅文字縮放仍允許必要的表單捲動以維持可操作性。

導覽、帳號選單、圖示、搜尋與勾選都由共用元件提供。設定視窗在 app 根層延遲載入，保留當前路由與內容；正常開關維持閱讀位置，實際變更字級時允許瀏覽器依新高度重排。標題與關閉按鈕固定在視窗頂部，捲動長設定清單仍可直接關閉。直接造訪舊 `/settings` 連結會於聊天背景開啟設定，維持書籤相容。管理員的使用者活動採原生 modal，所有頁籤維持相同寬高，姓名與 Mail 右側整合使用統計，窄螢幕將統計移到身分下方；長內容在固定閱讀區內捲動。群組編輯表單仍依內容使用適合的尺寸。稽核沿用系統日誌的共用 DataTable 與 DetailDrawer，桌面保留列表操作，窄螢幕轉為 modal；表單、細邊框與密度沿用相同共用元件。

對話輪次浮層以實際橫槓／輪次入口定位，限制在閱讀區及螢幕範圍；桌面在閱讀區右側中央，手機入口放在閱讀區右下，避開提問角色標籤。使用原生 popover top layer，Escape、點外部或離開預覽可關閉；動態文字以純文字摘要呈現。

長對話、程式碼高亮、Markdown 安全邊界與啟動請求的規則見 [聊天渲染](CHAT_RENDERING.md)。

`MarkdownView` 在安全解析後，將完成的 Mermaid fenced block 掛載到 renderer 自己產生的插槽；清單及引用中的圖表保留原有結構。對話、成果文件、使用者活動、分享與其他 Markdown 閱讀介面沿用同一元件。Mermaid 12.1.0、ELK 及圖表元件／樣式／專用圖示均延遲載入，沒有圖表便不下載引擎。使用 Neo 外觀、專案設計 token、系統字型與統一圖表字級，支援深色、圖表／原始碼切換、縮放、滑鼠拖曳、鍵盤縮放、展開和 SVG 下載。長圖表在自身畫布捲動，避免撐寬頁面；錯誤保留原始碼，串流未完成的 fence 不提早繪圖。

Mermaid 外觀集中在 `mermaid-theme.ts`，所有閱讀入口共用 Neo／ELK、14px 系統字體、細線與無陰影節點；一般矩形使用圓角，保留菱形判斷與特殊節點形狀。常用 classDef 名稱 `process`／`decision`／`error`／`result` 對應網站語意 token，避免模型輸出的亮黃、厚描邊與陰影破壞全站風格；其他自訂類別保留來源配色，原始碼不改寫。共用 SVG 處理器依背景與站點文字色計算 4.5:1 對比，不足時改用可讀的黑／白標籤，下載沿用相同結果。參考 [WCAG 文字對比](https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html)。

長圖在自己的畫布捲動，紙面明確包含縮放後尺寸及四邊留白，避免 grid 置中造成不可到達的下緣／右緣。字體就緒後只量測一次清理後的 SVG，將實際圖形邊界納入 viewBox。支援「適合寬度」與「全圖」：前者優先閱讀，後者查看完整關係；縮放範圍 5–400%，滑鼠拖曳、鍵盤與 SVG 下載共用同一尺寸資訊。

段落改寫、摘要、解釋、翻譯與另存成果共用 `MarkdownEditor`，預設呈現可閱讀的 Markdown，明確切換至原始文字編輯，保留複製、套用與儲存流程。段落工具使用可展開的固定閱讀區、折疊原文與固定操作區，長結果在內容區捲動；沿用 MarkdownView 的表格、程式碼、Mermaid 與安全邊界。`CompactDialog.opened` 提供原生視窗的共用生命週期，段落工具只在開啟時建立閱讀／編輯內容，關閉時銷毀閱讀器、圖表與 Blob URL，避免隱藏視窗持續渲染。

`ViewMotion` 以頁籤／檢視 key 觸發內容的淡入、位移與輕微縮放，使用者活動視窗維持固定外框。群組編輯等需要不同尺寸的 native dialog 使用 `DialogMotion`，只在檢視切換時量測並過渡寬高，避免每次輸入與載入都改變外框。快速連續切換取消舊動畫，關閉／銷毀會清理；作業系統或帳號偏好的減少動態設定立即停用動畫。焦點與 Escape 仍由原生 dialog 和共用檢視控制負責。

引擎渲染序列化，防止共用設定與主題互相覆蓋；版本檢查及 destroy 清理阻擋遲到結果，Blob URL 在替換與離開時釋放。strict 模式停用 authored click callbacks，所有 init／frontmatter 設定由站點固定；禁止圖表圖片節點在量測時發出請求。SVG 再經獨立 DOMPurify SVG allowlist 與 CSS resource 清理，以 Blob 圖片顯示，沒有第二個 active SVG trusted HTML boundary。CSP 只為圖片加上 `blob:`，script／connect／font／worker 仍限制同源。技術與外觀參考 [Mermaid 12.1.0](https://github.com/mermaid-js/mermaid/releases/tag/mermaid%4012.1.0)、[官方 API](https://mermaid.js.org/config/usage.html) 與 [主題配置](https://mermaid.js.org/config/theming.html)。

Mermaid 的 KaTeX npm 間接依賴透過有範圍的 override 固定為官方已修正的 0.18.2，處理套件依賴中的 [GHSA-238p-pmpm-9mq7](https://github.com/advisories/GHSA-238p-pmpm-9mq7)。此 override 不替換 Mermaid 發行檔內已打包的數學引擎；更新 Mermaid 時仍須檢查其發行檔與依賴，能由上游解決時移除 override。保留 Mermaid 12.1.0，不採用 npm audit 建議的舊版降級。時序圖的數學標籤使用瀏覽器原生 MathML，清理後只允許承載 MathML 的 foreignObject，沒有額外 CDN 或字型請求。

文字選取操作由 `TEXT_ACTIONS` 共用改寫、摘要、解釋、翻譯的名稱與語意圖示；聊天浮動工具依實際尺寸定位，窄螢幕換列並限制於可視範圍，支援方向鍵與 Esc。成果編輯器使用相同操作及 `TextTools`。解釋走既有模型授權、用量、取消與文字轉換端點，以淺白文字說明詞義及關係，未知背景不得臆測。翻譯使用 `ViewSwitch` 的 segment 外觀直接選四個語言，不使用選單；共用 aria-pressed、方向鍵及觸控目標。

CSP 禁 inline script；build 關閉 inlineCritical 避免 CSS loader 的 inline onload 被擋。瀏覽器測試同時檢查互動、實際 grid／字級／尺寸、隱藏模型與 motion。artifacts/screenshots 的聊天畫面使用明確的測試使用者；正式執行不填入測試身分或回答。
