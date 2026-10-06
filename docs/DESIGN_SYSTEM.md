# AI Nexus design tokens 與互動

介面以閱讀長對話為主：中性色工作區、清楚字級、緊湊 chrome；青綠訊號只用於生成狀態與 Context，用動效表達系統正在工作。所有主題、字級與主要尺寸集中在 `frontend/src/tokens.scss`。

全站採克制的中性色表面、細線分隔與文字層級。禁止以彩色側邊線、漸層背景、發光陰影、透視網格或懸浮卡片動效裝飾內容；色彩僅用於操作選取、焦點與真實狀態。低層次表面共用 `--surface-subtle`，由 surface／canvas 混色並在兩種主題映射。閱讀預覽以標頭分隔與字級展示效果；未讀通知使用中性底色、狀態點及「未讀」文字。數量徽章的 overlay 配合共用 `.icon-badge-anchor` 貼近實際圖示，操作面積仍由外層按鈕保留。

## 三層 token

| 層        | 例子                                                     | 修改原則                                           |
| --------- | -------------------------------------------------------- | -------------------------------------------------- |
| Primitive | `--p-stone-50`、`--p-font-reading`、`--p-space-4`        | 原始色階、rem 字級、4px 間距、圓角基礎             |
| Semantic  | `--canvas`、`--ink`、`--signal`、`--text-body`           | 指定用途；light／dark 重新映射，不在元件寫主題條件 |
| Component | `--topbar-height`、`--composer-width`、`--button-target` | 維持各元件與版面的可讀性及操作面積                 |

元件樣式使用 semantic／component 值。新增顏色先建立用途，才選 primitive；不要在各頁直接寫 hex 或重複字級。更換品牌色調整 `--accent`／`--accent-soft`／`--focus`／`--signal` 的 light 與 dark 映射。全站文字變大調整 font primitives；單一區域密度則調 component tokens。

## 文字與閱讀

根字級採瀏覽器預設 16px，不固定 html px，讓使用者縮放與偏好生效。UI 使用 Segoe UI／微軟正黑體；程式碼使用 Cascadia Code／系統 monospace，不依賴外部字型請求。

| 用途                         | Token                           | 預設（16px root）                        |
| ---------------------------- | ------------------------------- | ---------------------------------------- |
| 附註／工具列／模型與 Context | `--text-caption`                | 14px，介面輔助字級                       |
| 標籤／清單／標題輔助         | `--text-label`                  | 15px                                     |
| 一般 UI／表單                | `--text-ui`                     | 16px                                     |
| 對話正文／訊息輸入           | `--text-body`                   | 15px，1.2 行高                           |
| 小標題                       | `--text-heading`                | 18px                                     |
| 主要標題                     | font xl／2xl／3xl               | 20／24／32px                             |
| 工作區開場／登入主標         | `--text-display`／`--text-hero` | 26–34／36–58px 流動字級，使用 rem 上下限 |

正文色使用 ink，輔助文字使用 secondary／muted，不能以低對比淡字承載操作與狀態。配色以一般文字 WCAG AA 4.5:1 為驗證目標；focus 有 2px 可見輪廓。新文字／背景組合仍需實測對比，不能因 token 有色值就視為全部合格。

個人閱讀偏好可將 `--text-body` 調至 12–24px、`--line-reading` 調至 1–2.2；不縮小其他 UI token。彈出視窗統一以 `--dialog-width` 設定理想寬度，並受 viewport 邊界限制，不使用瀏覽器預設粗框或由內容推算的窄寬度。按鈕／圖示使用 inline-flex 對齊；勾選的 20px 指示器與文字同行，整列至少 44px 可操作。

`Icon` 使用 Lucide 1.52.0：語意名稱對應精選 SVG 資料，再由單一 Angular renderer 繪製，避免匯入整套圖示或保留每個圖示的 Angular component metadata。新增圖示只更新此對照；品牌、傅立葉畫布及資料圖表保留自身圖形語彙。

## 對話的空間分配

頂列 `--topbar-height=52px`，不重複頁面分類。正文寬 `--reading-width=52rem`，輸入區 `--composer-width=54rem`；gutter 在 16–48px 間響應調整。輸入框從單行自動增高，上限 min(192px, 25dvh)，長草稿在框內捲動。短草稿與正常狀態的 1280×768 桌面，對話 viewport 保留至少 70% 高度，瀏覽器測試檢查。

模型、思考、Context 在 composer 底部，送出／停止靠右。鎖定模型呈現系統政策；隱藏名稱時不在標頭或歷史補出實際模型。Context 展開顯示預估／輸出預留／裁切資訊，支援鍵盤與 Escape 返回焦點。手機 Context 只保留可點擊圓環，選項保留在輸入區，避免頂列擠壓對話。

主要按鈕操作面積 44px；窄螢幕導覽為 modal drawer，有 inert、Escape 與焦點返回。動作不只依 hover 可見；focus、停止、錯誤與斷線都是可辨識狀態。使用 Enter 送出、Shift+Enter 換行，中文 IME 組字中的 Enter 不送出。

## Motion tokens

提問泡泡的寬度、底色與邊框使用 `--message-user-width`、`--user-bubble`、`--user-bubble-border`；AI 正文保留左側閱讀線與角色標籤。對話定位使用 `--outline-space`，桌面預留側邊空間，手機將目錄入口放在閱讀區右下。摘要卡以實際觸發元素及閱讀區邊界定位，支援 hover 與 focus，跳轉遵循減少動態設定。

| Token                               | 預設                        | 使用                                 |
| ----------------------------------- | --------------------------- | ------------------------------------ |
| `--motion-fast`                     | 120ms                       | 快速提示                             |
| `--motion`                          | 200ms                       | hover／焦點／drawer／訊息進場        |
| `--motion-enter`                    | 320ms                       | 空白工作區淡入                       |
| `--motion-signal`                   | 1800ms                      | 生成訊號流／串流游標呼吸             |
| `--motion-generation`／`--motion-waiting` | 2100／2800ms               | 準備回答／排隊的三節短線淡亮         |
| `--motion-panel`                    | 240ms                       | dialog／選單與附件進場               |
| `--motion-stagger`／`--motion-draw` | 70／900ms                   | 建議卡片分段進場／SVG Nexus 線條繪製 |
| `--ease`                            | cubic-bezier(0.2,0.8,0.2,1) | 一般進場／過渡                       |

生成時上方使用較小、較淡的 SVG 雙軌訊號流動，訊息等待區使用三節短線依序淡亮（尺寸由 `--generation-indicator-size` 控制），兩者共用 signal 色與圓角筆畫但動態不同。composer 僅維持靜態狀態邊框，收到內容後移除等待訊號並顯示文字尾端游標，工作結束即停止。禁止未知進度的假百分比、會干擾閱讀的持續整頁動畫。串流沿用安全 Markdown 渲染，保留完成段落、僅更新尾段，維持輸入與捲動回應。

`generationStatus` 共用排隊、準備回答與輸出中的顯示規則，僅依伺服器 run 狀態及是否收到內容判定。思考強度不是實際推理階段，不以設定值宣稱「正在推理」。聊天上方的單一 status 區負責讀屏通知，訊息區的 `GenerationIndicator` 只呈現文字與裝飾訊號，避免重複播報。

系統 `prefers-reduced-motion` 或個人「減少動態效果」停止 signal／generation／cursor loop，進場與 transition 幾乎立即完成；顏色與文字狀態仍保留。兩種入口共用 `motion.scss` 的 reduced-motion mixin，響應規則隨元件樣式保存；鍵盤行為由共用 Disclosure、原生 dialog、CommandPalette 及頁面協調。

## 檔案與驗證

共用 `Select` 使用 combobox／listbox、可見 focus、方向鍵、typeahead 與原生 popover top layer，避免被側欄或 dialog 裁切；浮層依可用畫面翻轉並限制高度。個人設定只修改 semantic／component tokens，UI 最小字級與觸控目標保留。設定頁的分類側欄與內容寬度使用 `--settings-sidebar-width`、`--settings-content-width`。

`styles.scss` 只管理載入順序；`tokens.scss`／`base.scss` 管全域，`styles/` 依責任拆分 controls、shell、sidebar、welcome、messages、markdown、composer、dialogs、tools、attachments。每個檔案包含自身的響應規則；`login.scss` 管登入頁，`composer-controls.scss` 管模型／思考／Context，`motion.scss` 統一動效。UI 元件不複製 token，也不維持第二份桌面／手機對話選單。

工作區、dialog、範本、快捷指令、附件縮圖與列表高度使用 component tokens：`--welcome-width`、`--dialog-width`、`--library-width`、`--command-width`、`--attachment-thumb`、`--attachment-list-max`。主要 controls 維持 44px 最小目標；輸入自動增高讀取 CSS token 上下限，無需同步修改 JavaScript 常數。

改 token 後用 `scripts/Verify.ps1` 並看 artifacts screenshots，至少檢查 light／dark、375px 手機、1280×768、長回答／長草稿、Context popover、減少動態與鍵盤。瀏覽器測試檢查字級、對話可用高度、橫向溢出、隱藏模型、Context 與匯出行為；新視覺需人工檢查，不以 build 成功取代視覺驗收。

登入頁與側欄共用 `BrandWordmark`，以 N 向量標誌取代 Nexus 的字首；shared/graphics 的同一輪廓供 SVG 與傅立葉動畫使用。動畫依字形實際位置及尺寸縮回，再交由靜態 SVG 顯示，支援螢幕與字級變動。等弧長取樣與一次性 DFT，逐幀只繪製預算內的圓與軌跡；4.2 秒後完全停止。保留跳過／重播、背景分頁暫停、DPR 上限 2、ResizeObserver、淺／深色及減少動畫偏好，表單全程可用。登入頁兩側內容有寬度上限，寬螢幕向中央靠攏，手機採單欄。

## 共用元件與直接檢視

系統閱讀預設為 15px、1.2 倍行高、240px 側欄；前端預設與 tokens、後端 UserPreferences 常數、API 及資料庫 default constraint 同步。ReadingDefaults migration 只更新預設值，保留已保存的個人偏好。

`CountBadge` 是共用數量標示：`count`、`max`（預設 99）、`tone`、`size`、`overlay`、`dot`、`showZero`。零值預設隱藏，大於上限顯示 `99+`，負數／非有限值視為零；`neutral / info / success / warning / danger` 使用 semantic tokens（neutral 使用 `--secondary`，其餘為同名 token）與 `--status-on-color`，兩個主題使用各自的前景配對。尺寸由 `--count-badge-*` component tokens 管理。Overlay 的父控制須定位，可用 `--count-badge-ring` 配合背景。

數量預設是裝飾，由父按鈕的名稱／描述提供「125 則未讀通知」等完整脈絡；獨立使用可傳 `label` 作文字替代。共用 badge 不自帶 live region，各功能只在一個 contextual status 播報更新。側欄鈴鐺使用 info 表示未讀數量，事件 success／error 在通知中心分別對應 success／danger，不用清單單頁的嚴重性推測全通知匣。`/design` 可檢查各語意色、零值、超量及 dot／overlay。

平台管理頁右上方「介面元件」開啟 `/design`。此頁組合正式元件，提供淺色／深色局部預覽及目前 semantic tokens 的 JSON 匯出；不修改個人偏好，不呼叫模型或建立公司資料。只有具 admin 功能者顯示工作區，範例不含敏感資訊。

| 元件                             | 使用與互動                                                                                 |
| -------------------------------- | ------------------------------------------------------------------------------------------ |
| Select                           | 單選 combobox／listbox、方向鍵／Home／End／typeahead、搜尋、單行名稱、Enter 套用、Esc 關閉 |
| ActionMenu                       | 動作 menu、上下移動跳過停用、Esc 返回 trigger；危險操作仍進入確認                          |
| ConfirmDialog                    | 原生 modal、清楚名稱及描述、初始焦點放取消、Esc／關閉取消，結束返回先前焦點                |
| InlineTitle                      | 雙擊／F2／Enter 編輯，Enter／離開儲存、Esc 取消；版本 guard 與每個實例唯一 ID              |
| MarkdownView                     | 共用文字／表格／程式碼渲染及複製；HTML／外部圖片與危險 URL 受限                            |
| StreamingAnswer | 沿用 MarkdownView 的安全邊界；保留完成段落 DOM、僅解析尾段，短暫緩衝突發文字，減少動態時直接更新 |
| FileBrowser／LibraryPicker | 個人原檔卡片／列表、來源關聯、選取、伺服器分頁與可見的存取說明 |
| DocumentViewer／ReaderDialog | 共用原圖／PDF／文字預覽；精簡工具列、桌面近全螢幕、手機全螢幕、Esc 與焦點返回 |
| JobProgress／InferenceSignal     | 真實階段及完成單位、未知總量的不定進度、取消要求與完成分開、減少動態仍保留文字             |
| Checkbox／SearchField            | 原生語意、整列勾選、停用與焦點狀態、搜尋圖示與清除；管理／分享／來源共用                   |
| FeaturePage／WorkspaceSidebar／WorkspaceNavigation | 所有頁面共用側欄外框、品牌、分類四欄入口與帳號列；聊天投影操作與歷史，底部導覽預設收合 |
| AccountMenu／SettingsDialog      | 共用登入者選單；設定在當頁開啟，延遲載入、未儲存確認、焦點返回                             |
| GenerationIndicator／FocusComposer | 三節短線與可覆寫的狀態文字、可放大編輯的同步草稿；減少動態與 IME 規則共用                  |
| InfoPopover                      | 費用與用量資訊的原生 top layer；可見名稱、鍵盤開啟／Esc、邊界翻轉與焦點返回 |
| TrendChart                       | SVG 趨勢、滑鼠／鍵盤共用游標、Home／End、文字資料表；不依賴顏色辨識數值 |

主題 zone 使用 `[data-theme='light'|'dark']` 重新映射同一組 semantic tokens，避免元件複製 dark 條件。危險按鈕使用 `--danger-surface`／`--danger-text`，與錯誤文字 `--error` 分開，讓深色的提示色不會變成低對比按鈕。主要／次要操作與 quiet labels 為 15px，icon button 及歷史操作保留 44px 範圍。sr-only／visually-hidden 供螢幕閱讀器，儲存回饋不佔用對話版面。

改元件後至少在此頁檢查：light／dark 文字及按鈕對比、390px 捲動與浮層、Tab／方向鍵／Esc、確認視窗焦點返回、停用狀態及減少動態。Edge 測試會檢查已選定的文字／背景組合 ≥4.5:1、控制面積及頁面無橫向溢出；仍需人工檢視實際頁面，不能推定所有可能 token 組合都通過。

## 緊湊側欄與總覽

側欄品牌與帳號各保留單列，移除重複副標；`--sidebar-brand-gap`、品牌字級與內距集中在 tokens。工作區採 PanelsTopLeft，快捷指令採 SquareTerminal，避免不同操作共用相同符號。44px 點擊目標保持不變，增加空間優先縮減裝飾與重複資訊。

`aside[nxWorkspaceSidebar]` 是側欄外框的唯一實作，`styles/workspace-sidebar.scss` 管共同尺寸、品牌與區域分配；`styles/sidebar.scss` 只管理聊天內容與手機 drawer。所有側欄共用 heading 鈴鐺，位於 sidebar-toggle 左側，收合 rail 時垂直排列。聊天使用 `collapsibleNavigation`：收合工作區時顯示歷史與聊天工具，展開時導覽延伸至品牌列下方並暫時隱藏聊天內容；收合後原有搜尋、篩選與捲動狀態仍在。導航的展開狀態由側欄管理，不以 CSS 猜測子元件狀態。首頁、品牌與登入預設目的地共用 `core/workspace-home.ts` 的 `/dashboard`；登入後由目的頁載入自己的資料，不預先初始化聊天模型、歷史與附件。

`InferenceSignal` 的 SVG 使用 host 的實際寬高，host 不參與 flex shrink；`JobProgress` 的進度樣式由元件管理，文字與動畫各佔獨立區域，避免小尺寸 host 與較大 SVG 重疊。

`FeaturePage` 在主要捲動區內使用單一 `.feature-content` 容器設定內容上限與置中，說明、稽核子元件及統計卡共用同一條對齊線。捲動區使用 `min-height: 0`，並建立定位上下文，避免圖表的螢幕閱讀器標籤在 viewport 外產生第二個網頁捲軸。路由 host 保持 block；全頁主要內容只由 `.feature-main` 捲動。統計卡 `.usage-grid`／`.stat-card` 由 platform 樣式共用，依可用寬度自動換欄。頁面內容使用 opacity 進場，遵循既有減少動態規則。

## 共用捲軸與彈窗

全專案捲軸集中在 `styles/scrollbars.scss`，不引入捲動 JavaScript 或覆寫滑鼠滾輪行為。`--scrollbar-thumb`／`--scrollbar-thumb-hover` 隨主題映射；`--scrollbar-size=12px` 保留操作範圍，`--scrollbar-inset=3px` 形成 6px 圓角滑塊。Chromium／WebKit 使用透明軌道及 inset thumb；其他引擎使用標準 scrollbar properties。高對比 forced-colors 保留瀏覽器的原生捲軸。

Playwright 保留捲軸顯示，排除 headless 預設的 `--hide-scrollbars`，讓截圖與溢出檢查涵蓋使用者實際看到的滑塊與 gutter。

一般彈窗的原生 `<dialog>` 只負責圓角外框、top layer、焦點與關閉行為；內容放入共用 `.dialog-scroll`。`--dialog-inset` 留出外框與捲軸間距，內層負責最大高度、捲動與 overscroll containment，避免滑塊貼到圓角。`--dialog-padding` 控制桌面／手機內容內距；設定、使用者檢視與放大輸入這類分區彈窗保留各自的內部捲動區，套用同一份捲軸樣式。主要內容與 dialog body 使用 stable gutter 防止資料變多時寬度跳動，參考 [MDN scrollbar-gutter](https://developer.mozilla.org/en-US/docs/Web/CSS/Reference/Properties/scrollbar-gutter)。

稽核清單的文字、資源識別與異動內容由 computed presentation 在資料變更時建立，避免每次模板更新重複解析 JSON。共用 TrendChart 的數值 formatter 依幣別快取，滑鼠／鍵盤檢查資料點時不重建 Intl.NumberFormat；切換幣別後自動更新。

總覽採四張指標卡、可檢查節點的資料流向圖、費用趨勢與模型／使用者分布，使用既有 platform-card、form-input 與語意色。流向圖的位置表達文件→向量→回答的關係；只有真實 activeJobs／activeGenerations 會觸發訊號動畫，減少動態時靜態保留狀態。桌面多欄、手機單欄，使用主內容捲動，不放假即時數據或與工作無關的 3D 場景。

費用數字最多顯示小數八位，保持 token 單價的可讀性；不同幣別與成本類型分開呈現。空資料、未定價、尚未取得 usage、載入、錯誤都有明確文字，沒有把未知金額顯示為零。模型分布使用相同報表資料，總覽及聊天不另外計算費用。
