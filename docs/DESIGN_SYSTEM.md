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
| 附註／工具列／模型與 Context | `--text-caption`                | 14px，最小可見字級                       |
| 標籤／清單／標題輔助         | `--text-label`                  | 15px                                     |
| 一般 UI／表單                | `--text-ui`                     | 16px                                     |
| 對話正文／訊息輸入           | `--text-body`                   | 17px，1.8 行高                           |
| 小標題                       | `--text-heading`                | 18px                                     |
| 主要標題                     | font xl／2xl／3xl               | 20／24／32px                             |
| 工作台開場／登入主標         | `--text-display`／`--text-hero` | 26–34／36–58px 流動字級，使用 rem 上下限 |

正文色使用 ink，輔助文字使用 secondary／muted，不能以低對比淡字承載操作與狀態。配色以一般文字 WCAG AA 4.5:1 為驗證目標；focus 有 2px 可見輪廓。新文字／背景組合仍需實測對比，不能因 token 有色值就視為全部合格。

## 對話的空間分配

頂列 `--topbar-height=52px`，不重複頁面分類。正文寬 `--reading-width=52rem`，輸入區 `--composer-width=54rem`；gutter 在 16–48px 間響應調整。輸入框從單行自動增高，上限 min(192px, 25dvh)，長草稿在框內捲動。短草稿與正常狀態的 1280×768 桌面，對話 viewport 保留至少 70% 高度，瀏覽器測試檢查。

模型、思考、Context 在 composer 底部，送出／停止靠右。鎖定模型呈現系統政策；隱藏名稱時不在標頭或歷史補出實際模型。Context 展開顯示預估／輸出預留／裁切資訊，支援鍵盤與 Escape 返回焦點。手機 Context 只保留可點擊圓環，選項保留在輸入區，避免頂列擠壓對話。

主要按鈕操作面積 44px；窄螢幕導覽為 modal drawer，有 inert、Escape 與焦點返回。動作不只依 hover 可見；focus、停止、錯誤與斷線都是可辨識狀態。使用 Enter 送出、Shift+Enter 換行，中文 IME 組字中的 Enter 不送出。

## Motion tokens

提問泡泡的寬度、底色與邊框使用 `--message-user-width`、`--user-bubble`、`--user-bubble-border`；AI 正文保留左側閱讀線與角色標籤。對話定位使用 `--outline-space`、`--outline-card-width`，桌面預留側邊空間，手機將目錄入口放在頂列。摘要卡支援 hover 與 focus，跳轉遵循減少動態設定。

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

`styles.scss` 只管理載入順序；`tokens.scss`／`base.scss` 管全域，`styles/` 依責任拆分 controls、shell、sidebar、welcome、messages、markdown、composer、dialogs、tools、attachments。每個檔案包含自身的響應規則；`login.scss` 管登入頁，`composer-controls.scss` 管模型／思考／Context，`motion.scss` 統一動效。UI 元件不複製 token，也不維持第二份桌面／手機對話選單。

工作台、dialog、範本、快捷指令、附件縮圖與列表高度使用 component tokens：`--welcome-width`、`--dialog-width`、`--library-width`、`--command-width`、`--attachment-thumb`、`--attachment-list-max`。主要 controls 維持 44px 最小目標；輸入自動增高讀取 CSS token 上下限，無需同步修改 JavaScript 常數。

改 token 後用 `scripts/Verify.ps1` 並看 artifacts screenshots，至少檢查 light／dark、375px 手機、1280×768、長回答／長草稿、Context popover、減少動態與鍵盤。瀏覽器測試檢查字級、對話可用高度、橫向溢出、隱藏模型、Context 與匯出行為；新視覺需人工檢查，不以 build 成功取代視覺驗收。
