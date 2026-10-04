# 工作台互動與渲染

視覺、尺寸、字級與 motion 的維護規範見 [DESIGN_SYSTEM](DESIGN_SYSTEM.md)。

聊天 lazy-loaded，Signal Forms、zoneless 與 OnPush。IME composition／229 Enter 不送出，Shift+Enter 換行；草稿自動增高，讀取歷史時保持捲動位置並提供「回到最新」。手機 drawer 有關閉、Escape、inert 與焦點返回。

登入頁延續工作台 token：桌面雙欄、手機單欄；自有傅立葉標誌動畫可跳過／重播，遵循減少動態，表單全程可用。提交後清除個人密碼，返回位置只接受列入白名單的本站功能／閱讀器路徑；登入／登出重設 ChatStore。SQL、AD 服務密碼與 Google key 在後端。

登入動畫先於中央描繪 N，完成後縮合至固定品牌錨點，最後依序顯示品牌文字、標題、說明與頁尾。畫布覆蓋品牌欄，不參與高度計算；常見桌面、375×667 與 320×568 手機保持一頁。低高度／大幅文字縮放仍允許必要的表單捲動以維持可操作性。

導覽、帳號選單、圖示、搜尋與勾選都由共用元件提供。設定視窗在 app 根層延遲載入，保留當前路由與內容；正常開關維持閱讀位置，實際變更字級時允許瀏覽器依新高度重排。標題與關閉按鈕固定在視窗頂部，捲動長設定清單仍可直接關閉。直接造訪舊 `/settings` 連結會於聊天背景開啟設定，維持書籤相容。管理員的使用者活動與稽核視窗也共用相同表單、細邊框及原生 modal 行為。

對話輪次浮層以實際橫槓／輪次入口定位，限制在閱讀區及螢幕範圍；桌面在閱讀區右側中央，手機入口放在閱讀區右下，避開提問角色標籤。使用原生 popover top layer，Escape、點外部或離開預覽可關閉；動態文字以純文字摘要呈現。

Markdown 禁 raw HTML、external images、危險 URL；解析後經 DOMPurify tag／attribute allowlist，才進入 Angular trusted HTML boundary，以保留 code-copy button。不得把其他 HTML 傳到該 boundary。串流時先顯示純文字，完成／取消後渲染 Markdown，避免每 token 重算。

CSP 禁 inline script；build 關閉 inlineCritical 避免 CSS loader 的 inline onload 被擋。瀏覽器測試同時檢查互動、實際 grid／字級／尺寸、隱藏模型與 motion。artifacts/screenshots 的聊天畫面使用明確的測試使用者；正式執行不填入測試身分或回答。
