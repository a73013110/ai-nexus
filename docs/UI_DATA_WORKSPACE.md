# 共用資料工作區

系統日誌是緊湊資料介面的基準頁。全站功能沿用既有中性色、字型與側欄，透過共用密度與原生語意減少空白；欄位、狀態與完整診斷資料仍可取得。聊天與文件正文維持個人閱讀設定，內容操作區與原生彈窗繼承同一套介面 tokens。

## 設計參考

- [HP Workforce Experience Platform，UX Design Awards 2025](https://workforceexperience.hp.com/blog/wxp-ux-design-awards-2025/)：統一管理工具與共用設計元件。
- [AWS Uno，UX Design Awards 2025](https://ux-design-awards.com/winners/2025-1-uno)：資料整合與清楚的操作層級。
- [Carbon Data Table](https://www.carbondesignsystem.com/building-blocks/core/components/data-table/guidelines)：不同資料密度、表頭與工具列。
- [WAI APG 日期選擇](https://www.w3.org/WAI/ARIA/apg/patterns/dialog-modal/examples/datepicker-dialog/)：單一日曆 Tab 停駐點、方向鍵／Home／End、換月與焦點回復。此處採非 modal 原生 popover，保留正常 Tab 順序。
- [Fluent 2 Drawer](https://fluent2.microsoft.design/components/web/react/core/drawer/usage)：資訊型詳情可保留背景操作；窄螢幕使用 modal。

這些參考用於資訊架構與互動選擇；實作保留專案既有 Angular、原生 dialog／popover 與 Lucide 圖示。

## 元件責任

| 元件／樣式                                     | 用途                                                                                                                                                                                         |
| ---------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| FeaturePage                                    | 預設 compact 與 wide；只改內容密度，側欄與閱讀正文有各自尺度。`layoutMode="data"` 讓資料表填滿剩餘高度，大量篩選展開時外層仍可捲動。必要時明確指定 comfortable 或窄版。                      |
| `.ui-density-compact`                          | 控制項、字級、卡片、資源列、區塊與彈窗的 component tokens 有同一個密度來源。桌面控制項 34px，640px 以下與觸控指標保留 44px；手機輸入字級至少 16px。                                          |
| FilterPanel                                    | 共用「篩選」標頭、操作插槽與面板。投影欄位可使用 `.ui-filter-grid` 或功能自己的欄位配置；原生 form、查詢、驗證與草稿狀態仍由功能頁負責。                                                     |
| SearchField／ViewSwitch                        | 共用搜尋、清除與互斥檢視。ViewSwitch 使用 group 與 aria-pressed，支援方向鍵／Home／End；連結 tabpanel 的內容使用 Tabs。切換資料或查詢的副作用仍由功能頁負責。                                |
| CompactDialog                                  | `dialog[nxCompactDialog]` 套用共用密度，開啟時從 h2 建立缺少的可存取名稱，尊重明確的 aria-label／aria-labelledby；不接管原生 focus、Escape 或業務狀態。                                      |
| Field／`.ui-field`                             | 原生 input／select／textarea 的語意、label 與 Angular forms；統一邊框、間距和焦點。                                                                                                          |
| Select／共用按鈕                               | 沿用既有單選、原生 popover、鍵盤與停用狀態；密度由 tokens 繼承。                                                                                                                             |
| DateTimePicker                                 | 原生 popover、直接輸入、年月跳轉、日曆鍵盤、時分秒、min／max 與日期驗證；calendarSystem 支援 gregory／roc，withTime 可切換純日期。邊界使用西元 ISO wall-clock 值，民國只影響顯示與輸入轉換。 |
| DataTable／DataTableColumn                     | 投影原生 table，提供獨立捲動、固定表頭、busy、欄位顯示與還原。col／th／td 共用欄位 ID；preferenceKey 可保存經驗證的顯示偏好，核心欄位可禁止隱藏。                                            |
| DataTableRow／TableSortHeader／TablePagination | 整列指標點選，保留巢狀控制項與文字選取；事件按鈕支援上下鍵／Home／End。排序表頭提供 aria-sort，頁尾支援 25／50／100 筆與上一頁／下一頁；資料與排序查詢仍由業務頁負責。                       |
| StatusBadge                                    | neutral／info／success／warning／danger，文字與圓點同時表達狀態。                                                                                                                            |
| DetailDrawer                                   | 桌面 modeless、1023px 以下原生 modal；固定標頭、單一內容捲動區、關閉／展開、Escape、回復焦點與響應模式切換。                                                                                 |
| Tabs                                           | 單一可 Tab 聚焦的標籤；左右鍵、Home／End，搭配 `tabId`／`panelId` 連接 tabpanel。                                                                                                            |
| CodeBlock                                      | 純文字內容、換行、共用 CopyFeedback；不插入 HTML，長內容使用面板的捲動區。                                                                                                                   |
| `.ui-description-list`／`.ui-timeline`         | 共用詳細欄位與事件流程排版，不在每個功能重複定義。                                                                                                                                           |
| ConfirmDialog／NameDialog                      | 預設緊湊樣式；`[compact]="false"` 取消元件自己的緊湊 scope，繼承所在版面密度。                                                                                                               |

功能頁只保存查詢與權限邏輯，不把日誌等級、API 或游標塞入共用元件。`/design` 的範例直接組合正式元件，包含選填日期與鍵盤檢視切換。DateTimePicker 預設必填；選填日期指定 `[required]="false"`，清空時發出空值且不顯示必填錯誤。

`Card` 以 `nxCard` 套用共用表面、細框、圓角與 12px 內距；保留 article、section、fieldset 等原生語意，功能自己管理 grid／flex、點選與狀態。圖片卡片透過 `--card-padding: 0` 保留滿版封面，輔助表面透過 `--card-background` 使用既有 semantic token，不重複定義整份卡片 CSS。

`EmptyState` 統一空白與載入內容的間距、圖示與字級，投影文字及功能操作，不接管請求或錯誤。樣式限於 `.ui-empty-state`，避免與聊天歡迎頁的 `.empty-state` 混用。JobProgress 共用真實階段、StatusBadge 與完成單位；通知時間與動作排列在同一列，文字沿用 13px／12px 介面尺度。

緊湊 scope 同時設定 tokens 和實際 13px 字級／介面行高，避免未指定字級的標籤繼承 body 的 16px。章節標題預設 16px、頁首 24px。Field 與章節標題預設使用低 specificity 選擇器，Markdown、評測答案、文件純文字與長文編輯器明確使用 `--text-body`／`--line-reading`，保留個人閱讀設定。

DateTimePicker 內的月份使用共用 Select，年月與時間輸入使用 Field。巢狀選單沿用鍵盤、Esc 與 top layer；換月只更新日曆檢視，日期仍在套用時送出，取消保留原值。

ViewSwitch 同時支援文字、圖示與個別停用選項；方向鍵會跳過停用項目。檔案庫的網格／清單、閱讀器的原稿／擷取文字、成果的閱讀／編輯／並排及工作臺的主題預覽沿用同一個元件；閱讀器搜尋沿用 SearchField，移除自製切換與搜尋框樣式。設定彈窗也直接繼承 Card 的內距，不另外覆寫。

## 全站適用範圍

| 入口             | 共用配置與相關介面                                                                           |
| ---------------- | -------------------------------------------------------------------------------------------- |
| 總覽             | 頁首、篩選、日期、卡片、章節標頭、費用資料表與用量檢視                                       |
| 對話             | 選單、重新命名／確認、對話設定、提示詞、分享與專注編輯彈窗；正文和輸入閱讀設定維持獨立       |
| 檔案庫           | 篩選與分類切換、資源卡片／列、檔案選取、加入知識庫、分享與刪除確認                           |
| 專案             | 列表搜尋、章節標頭、摘要／資源列、專案表單與內容選取彈窗                                     |
| 知識庫／閱讀器   | 搜尋、資源列、建立與文字來源編輯、文件檢視及閱讀彈窗；正文與 PDF 比例維持獨立                |
| 成果文件         | 列表搜尋、版本與工具表單、資源操作與分享；內容編輯／閱讀沿用原閱讀尺度                       |
| 分享             | 收到／送出檢視、資源列、分享表單與確認                                                       |
| 品質評測         | 互斥檢視、資料集與題目表單、評測執行／答案比較、評分彈窗、檢索評測及搜尋結果卡片             |
| 程式庫           | 程式庫連接／搜尋與列表、檔案樹／議題／Review 切換、提交選取、執行進度、歷史與完整報告        |
| 背景任務         | 篩選與狀態切換、任務卡片、共用進度、重試／取消確認                                           |
| 資料來源         | 篩選搜尋、來源目錄、結果列、閱讀預覽、擷取進度與確認彈窗                                     |
| 平台管理         | 主檢視、群組區段切換、資源列與編輯表單、使用者檢視、模型用量資料表、費率與檢索管理           |
| 活動稽核         | 獨立工作區、共用篩選與日期、游標資料表、前後差異抽屜及系統日誌查證連結                       |
| 系統日誌         | 共用篩選、日期、資料表與詳情抽屜回歸；原游標、欄位偏好、權限及查詢快照持續適用               |
| 通知／設定／帳號 | 通知面板的密度與捲動、設定搜尋／導覽／表單、帳號原生 popover 與共用選單；側欄觸發區維持 44px |

管理表單的原生控制項統一使用 nxField，搜尋使用 SearchField；舊的 page-search、page-tabs、platform-card、各功能表單邊框、表格表頭及重複空白狀態樣式已移除。Card、EmptyState 與資源列使用共用語意 token，功能只保留自身配置。資料用途決定適合的列表、卡片、表格或閱讀版型，不把所有功能強制改成日誌資料表。

時間顯示統一呼叫共用 formatDate，使用台北時區及 yyyy/MM/dd HH:mm:ss，不另建各功能的 Intl formatter。費用生效時間也使用共用日期元件與 parseDateTimeInput，明確由台北 wall-clock 轉 UTC；空白仍代表儲存時生效。稽核的選填日期在無效時暫停自動查詢，清空則移除日期條件，結束日期沿用含當日的查詢邊界。

## 資料介面的選擇與操作層級

表格用於橫向比較固定欄位；長文字及非必要資訊放在選取後的詳細介面，減少列表列高變動。以下是依目前資料與操作需求做出的選擇，沿用既有 DataTable／DetailDrawer，不引入第二套表格或抽屜。

| 功能                                  | 配置                         | 選擇原因                                                                                     |
| ------------------------------------- | ---------------------------- | -------------------------------------------------------------------------------------------- |
| 系統日誌、活動稽核                    | DataTable + DetailDrawer     | 需要掃描大量按時間排列的紀錄，再逐筆查看完整資訊與前後差異；桌面保留列表操作，手機採 modal。 |
| 使用者                                | DataTable + 使用者活動視窗   | 帳號、狀態、角色、用量與容量可逐欄比較；三個頁籤維持相同的寬版視窗，內容區獨立捲動。         |
| 功能配置                              | DataTable + CompactDialog    | 名稱、路由、狀態與順序適合並列比較，僅三個設定的編輯視窗使用共用緊湊寬度。                   |
| 個人／群組模型政策                    | DataTable + Field／Checkbox  | 模型與額度對齊，欄位名稱只放在表頭，生效政策與剩餘額度放回模型欄，避免每筆獨佔寬大的卡片。   |
| 平台／總覽用量報表                    | 現有 DataTable 與圖表        | 保留聚合、日期與幣別語意，沒有逐筆事件詳情便不增加空抽屜。                                   |
| 專案、文件、分享、任務與程式庫 Review | 資源清單、卡片與專屬閱讀介面 | 文件、來源、進度及報告的內容差異大，保留適合閱讀與操作的配置。                               |

稽核沿用伺服器的每批 100 筆與 before 游標，和使用者列表共用 TablePagination 的固定 100 筆模式，畫面只呈現目前頁面。稽核保留已讀頁面，返回時不重複查詢；CSV 匯出包括所有已載入紀錄，不推測總筆數。翻頁重設列表位置並關閉舊詳情，修改查詢條件另清除頁面快取及匯出紀錄。抽屜支援本頁上一筆／下一筆、整列點選、鍵盤與焦點返回；完整資料以文字呈現，不將紀錄當作 HTML。

平台管理的七個檢視依「帳號與授權」「運作與紀錄」分組，仍可直接切換，不增加操作步驟。右上「管理工具」使用支援具名 trigger 的共用 ActionMenu，依權限提供日誌、費用總覽、價格與元件工作臺；平台用量的費用入口保留在自身操作區。新增、搜尋、匯出與儲存屬於目前資料或表單，不混入跨頁工具。

對話開場沿用 24px 標題、13px 操作與 12px 註記，起點卡片共用 nxCard；移除裝飾圖形、序號與重複說明。尚無訊息時，標題、輸入框及起點依序在主區域置中；起點在輸入框下方，桌面三欄、手機單欄。出現訊息後回到正常閱讀與底部輸入配置。長草稿／附件仍可由內容區捲動，正文與輸入字級保持個人閱讀偏好。

一般平台表單透過共用 `.platform-form` 管理欄位間距，避免同時疊加 workspace label margin 與 form gap。群組短表單依內容收縮；使用者活動視窗的對話、AI 模型及附件容量固定同一寬高（桌面最大 72rem × 860px，依視窗縮小），切換不重開視窗、不改變外框尺寸。身分與三項統計在桌面頁首併排，手機統計排在身分下方，讓閱讀區騰出原先整列統計的高度。長內容獨立捲動，分類和儲存操作保持可取得；沒有尺寸變化便不加入縮放動畫。使用者活動樣式由 `AdminUserInspector` 載入，移出全站初始樣式，沿用原本的具名 selector，不疊加另一組覆寫。上述配置選擇參考 [Carbon Data Table](https://www.carbondesignsystem.com/building-blocks/core/components/data-table/guidelines) 與 [Fluent 2 Drawer](https://fluent2.microsoft.design/components/web/react/core/drawer/usage)，實際範圍由專案資料和流程判斷。

## 日誌的狀態與資料

查詢區塊採「篩選」與「查詢」的共用用語。篩選與顯示都以 Asia/Taipei 為準，24 小時制 yyyy/MM/dd HH:mm:ss，送出時轉 UTC；日期不能由瀏覽器自行正規化成另一個日期。查詢快照與尚未送出的欄位分開，換頁／匯出沿用同一快照；排序／每頁筆數也沿用已送出的篩選，重設游標。時間排序由伺服器套用於完整查詢範圍，以時間＋LogId 保持穩定分頁，游標綁定方向與每頁筆數。不推測未知的總筆數。

詳情立即在側欄開啟。單筆詳情與關聯流程獨立載入、分別顯示失敗狀態；關閉、改查詢或切換事件會取消舊請求，版本檢查阻擋遲到回應。桌面可直接選另一列，或使用面板的上一筆／下一筆。

列表以簡短模組名稱與截斷訊息減少行高；原文可由 title 與可鍵盤開啟的詳情取得。完整查證代碼仍保留於 DOM，複製只接受驗證過的代碼。權限不足時不顯示詳情／匯出操作，後端授權維持原有規則。

側欄依 Router UrlTree 的 segment 比對選取最深的可見功能；query、fragment、matrix parameters 不影響選取。同層只有一項 `aria-current="page"`，功能自己的詳細路由仍屬於該功能。

## 驗證

`system-logs.spec.ts` 驗證大量資料、導覽唯一選取、實際資料列密度、固定表頭、保留位置、桌面／窄螢幕面板切換、鍵盤、複製、游標與查詢快照、關聯失敗、取消請求，以及深色與權限。`workspace-navigation.spec.ts` 驗證巢狀路徑與查詢參數。`format.spec.ts` 驗證 Taipei 日期跨 UTC 日界。

`design.spec.ts` 驗證共用密度、檢視切換鍵盤、選填日期與手機閱讀尺度；`calendar.spec.ts` 檢查月份的共用選單、巢狀 Esc、鍵盤選取、取消及手機觸控。`workspace-experience.spec.ts` 檢查通知內文／時間字級與列高、背景任務卡片／狀態，以及通知到任務和 Review 報告的導覽。

`expectCompactWorkspace` 在有資料的各功能流程中檢查實際繼承字級、頁首、可見卡片的內距及 nxCard 指令是否已生效，涵蓋總覽、檔案、專案、知識庫、成果、分享、品質、程式庫 Review、背景任務、來源、管理、日誌與元件工作臺；手機與桌面沿用同一份密度契約。`settings.spec.ts` 檢查七個設定分類及閱讀偏好不影響操作尺度；`admin.spec.ts` 另檢查無效日期、清除條件與台北日界；`connected-workspace.spec.ts` 檢查費率與查詢快照；`files.spec.ts` 檢查手機來源選項；`artifacts.spec.ts` 檢查長文編輯器的閱讀字級與高度。其餘功能沿用各入口的流程、權限、彈窗與響應版面回歸測試，並檢視實際截圖。

真實 Angular／API／SQLite 的診斷遮罩、稽核及匯出驗收沿用 `scripts/Verify.ps1 -Browser -Performance`。
