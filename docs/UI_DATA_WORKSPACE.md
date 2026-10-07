# 共用資料工作區

系統日誌是緊湊資料介面的第一個使用頁。沿用既有中性色、字型與側欄，透過共用密度與原生語意減少空白；欄位、狀態與完整診斷資料仍可取得。

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
| FeaturePage                                    | `density="compact"` 只改內容密度，`wide` 使用可用寬度，`layoutMode="data"` 讓資料表填滿剩餘高度。大量篩選展開時外層仍可捲動。                                                                |
| `.ui-density-compact`                          | 使用共用 control、button 與 table tokens；桌面控制項 34px，觸控指標保留 44px。                                                                                                               |
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
| ConfirmDialog／NameDialog                      | 預設緊湊樣式，可用 `[compact]="false"` 保留一般密度。                                                                                                                                        |

功能頁只保存查詢與權限邏輯，不把日誌等級、API 或游標塞入共用元件。`/design` 的範例直接組合正式元件。

## 日誌的狀態與資料

查詢區塊採「篩選」與「查詢」的共用用語。篩選與顯示都以 Asia/Taipei 為準，24 小時制 yyyy/MM/dd HH:mm:ss，送出時轉 UTC；日期不能由瀏覽器自行正規化成另一個日期。查詢快照與尚未送出的欄位分開，換頁／匯出沿用同一快照；排序／每頁筆數也沿用已送出的篩選，重設游標。時間排序由伺服器套用於完整查詢範圍，以時間＋LogId 保持穩定分頁，游標綁定方向與每頁筆數。不推測未知的總筆數。

詳情立即在側欄開啟。單筆詳情與關聯流程獨立載入、分別顯示失敗狀態；關閉、改查詢或切換事件會取消舊請求，版本檢查阻擋遲到回應。桌面可直接選另一列，或使用面板的上一筆／下一筆。

列表以簡短模組名稱與截斷訊息減少行高；原文可由 title 與可鍵盤開啟的詳情取得。完整查證代碼仍保留於 DOM，複製只接受驗證過的代碼。權限不足時不顯示詳情／匯出操作，後端授權維持原有規則。

側欄依 Router UrlTree 的 segment 比對選取最深的可見功能；query、fragment、matrix parameters 不影響選取。同層只有一項 `aria-current="page"`，功能自己的詳細路由仍屬於該功能。

## 驗證

`system-logs.spec.ts` 驗證大量資料、導覽唯一選取、實際資料列密度、固定表頭、保留位置、桌面／窄螢幕面板切換、鍵盤、複製、游標與查詢快照、關聯失敗、取消請求，以及深色與權限。`workspace-navigation.spec.ts` 驗證巢狀路徑與查詢參數。`format.spec.ts` 驗證 Taipei 日期跨 UTC 日界。

真實 Angular／API／SQLite 的診斷遮罩、稽核及匯出驗收沿用 `scripts/Test-Diagnostics.ps1 -Browser -Performance`。
