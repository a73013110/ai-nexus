# AI Nexus design tokens 與互動

介面以閱讀長對話為主：中性色工作台、清楚字級、緊湊 chrome；青綠訊號只用於生成狀態與 Context，用動效表達系統正在工作。所有主題、字級與主要尺寸集中在 `frontend/src/tokens.scss`。

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
| 對話正文／訊息輸入           | `--text-body`                   | 17px，1.8 行高                           |
| 小標題                       | `--text-heading`                | 18px                                     |
| 主要標題                     | font xl／2xl／3xl               | 20／24／32px                             |
| 工作台開場／登入主標         | `--text-display`／`--text-hero` | 26–34／36–58px 流動字級，使用 rem 上下限 |

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
| `--motion-enter`                    | 320ms                       | 空白工作台淡入                       |
| `--motion-signal`                   | 1800ms                      | 生成訊號流／串流游標呼吸             |
| `--motion-panel`                    | 240ms                       | dialog／選單與附件進場               |
| `--motion-stagger`／`--motion-draw` | 70／900ms                   | 建議卡片分段進場／SVG Nexus 線條繪製 |
| `--ease`                            | cubic-bezier(0.2,0.8,0.2,1) | 一般進場／過渡                       |

生成時 SVG 雙軌訊號流動、composer 邊框狀態色與文字尾端游標；工作結束即停止。禁止未知進度的假百分比、會干擾閱讀的持續整頁動畫。串流時先顯示純文字，結束再渲染 Markdown，維持輸入與捲動回應。

系統 `prefers-reduced-motion` 或個人「減少動態效果」停止 signal／cursor loop，進場與 transition 幾乎立即完成；顏色與文字狀態仍保留。motion 與全域 reduced-motion 在 `motion.scss`，響應規則隨元件樣式保存；鍵盤行為由共用 Disclosure、原生 dialog、CommandPalette 及頁面協調。

## 檔案與驗證

共用 `Select` 使用 combobox／listbox、可見 focus、方向鍵、typeahead 與原生 popover top layer，避免被側欄或 dialog 裁切；浮層依可用畫面翻轉並限制高度。個人設定只修改 semantic／component tokens，UI 最小字級與觸控目標保留。設定頁的分類側欄與內容寬度使用 `--settings-sidebar-width`、`--settings-content-width`。

`styles.scss` 只管理載入順序；`tokens.scss`／`base.scss` 管全域，`styles/` 依責任拆分 controls、shell、sidebar、welcome、messages、markdown、composer、dialogs、tools、attachments。每個檔案包含自身的響應規則；`login.scss` 管登入頁，`composer-controls.scss` 管模型／思考／Context，`motion.scss` 統一動效。UI 元件不複製 token，也不維持第二份桌面／手機對話選單。

工作台、dialog、範本、快捷指令、附件縮圖與列表高度使用 component tokens：`--welcome-width`、`--dialog-width`、`--library-width`、`--command-width`、`--attachment-thumb`、`--attachment-list-max`。主要 controls 維持 44px 最小目標；輸入自動增高讀取 CSS token 上下限，無需同步修改 JavaScript 常數。

改 token 後用 `scripts/Verify.ps1` 並看 artifacts screenshots，至少檢查 light／dark、375px 手機、1280×768、長回答／長草稿、Context popover、減少動態與鍵盤。瀏覽器測試檢查字級、對話可用高度、橫向溢出、隱藏模型、Context 與匯出行為；新視覺需人工檢查，不以 build 成功取代視覺驗收。

登入頁的傅立葉標誌使用自有 N 輪廓、等弧長取樣與一次性 DFT，逐幀只繪製預算內的圓與軌跡。4.2 秒後完全停止；支援跳過／重播、背景分頁暫停、DPR 上限 2、ResizeObserver、淺／深色及減少動畫偏好。手機版縮成品牌旁的圖形，表單全程可用。繪圖原始碼位於 shared/graphics，避免動畫生命週期與登入驗證耦合。

## 共用元件與直接檢視

平台管理頁右上方「介面元件」開啟 `/design`。此頁組合正式元件，提供淺色／深色局部預覽及目前 semantic tokens 的 JSON 匯出；不修改個人偏好，不呼叫模型或建立公司資料。只有具 admin 功能者顯示工作台，範例不含敏感資訊。

| 元件                             | 使用與互動                                                                                 |
| -------------------------------- | ------------------------------------------------------------------------------------------ |
| Select                           | 單選 combobox／listbox、方向鍵／Home／End／typeahead、搜尋、單行名稱、Enter 套用、Esc 關閉 |
| ActionMenu                       | 動作 menu、上下移動跳過停用、Esc 返回 trigger；危險操作仍進入確認                          |
| ConfirmDialog                    | 原生 modal、清楚名稱及描述、初始焦點放取消、Esc／關閉取消，結束返回先前焦點                |
| InlineTitle                      | 雙擊／F2／Enter 編輯，Enter／離開儲存、Esc 取消；版本 guard 與每個實例唯一 ID              |
| MarkdownView                     | 共用文字／表格／程式碼渲染及複製；HTML／外部圖片與危險 URL 受限                            |
| JobProgress／InferenceSignal     | 真實階段及完成單位、未知總量的不定進度、取消要求與完成分開、減少動態仍保留文字             |
| Checkbox／SearchField            | 原生語意、整列勾選、停用與焦點狀態、搜尋圖示與清除；管理／分享／來源共用                   |
| FeaturePage／WorkspaceNavigation | 所有頁面共用分類四欄入口與完整 aria-label；聊天底部預設收合，歷史為主                      |
| AccountMenu／SettingsDialog      | 共用登入者選單；設定在當頁開啟，延遲載入、未儲存確認、焦點返回                             |
| ThinkingIndicator／FocusComposer | 不定進度訊號波形及可放大編輯的同步草稿；減少動態與 IME 規則共用                            |

主題 zone 使用 `[data-theme='light'|'dark']` 重新映射同一組 semantic tokens，避免元件複製 dark 條件。危險按鈕使用 `--danger-surface`／`--danger-text`，與錯誤文字 `--error` 分開，讓深色的提示色不會變成低對比按鈕。主要／次要操作與 quiet labels 為 15px，icon button 及歷史操作保留 44px 範圍。sr-only／visually-hidden 供螢幕閱讀器，儲存回饋不佔用對話版面。

改元件後至少在此頁檢查：light／dark 文字及按鈕對比、390px 捲動與浮層、Tab／方向鍵／Esc、確認視窗焦點返回、停用狀態及減少動態。Edge 測試會檢查已選定的文字／背景組合 ≥4.5:1、控制面積及頁面無橫向溢出；仍需人工檢視實際頁面，不能推定所有可能 token 組合都通過。
