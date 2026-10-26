# 出勤的表單內容

結論：出勤紀錄點單號開啟對話框，顯示公文系統那張假單或出差單的內容、簽核流程與附件（唯讀）。後端是 `GET /api/v1/attendance-forms/{desNo}`（[`GetGdForm`](../../backend/src/AiNexus.Features/Office/Attendance/Forms/GetGdForm.cs)），形狀見 `contracts/openapi.json` 的 `GdFormDetailDto`；授權、連線與降級見 [出勤與公文系統 GD](attendance-gd.md)。

## 權限

- 與出勤紀錄相同：看得到申請人出勤紀錄的人（`attendance.records` 的資料範圍）就看得到這張單與它的附件，不另設功能。
- 只開放出勤紀錄會列出的單（核准、流程中）；暫存、退回、銷單、不存在、範圍外一律回 404 `attendance_gd_form_not_found`，不讓人以單號猜出別人有沒有請假。
- 申請人對不到使用者時（沒有 AD 帳號），只有 `all` 範圍看得到，與出勤紀錄一致。

## 內容

- 請假單：假別、起訖、時數（8 小時一天）、事由、代理人、證明文件種類（`AttKind`）、喪假的親屬關係（`BereaveType` 是選項索引）。
- 出差單：事由、地點、交通工具、研習類別（`StudyMode` 為真時）、申請人分機。
- 畫面：標題寫假別（出差寫事由）與狀態，第二行是種類、單號與申請時間；主欄是期間摘要（起訖與時數）、事由、附件、簽核流程，側欄是申請人、代理人與單據欄位，捲動時側欄固定在頂端。日期一律帶年：單據可能跨年度，翻查前一年的單也分得清。
- 跨期間假單（`DayOfWeek` 有值）另回 `Form_Leave_Group` 的各段；期間摘要下方是「每週一 09:00–12:00」這種規則（同星期、同時段合成一條），再下方每月一張小月曆，上緣色條是上午、下緣是下午，點開的那天加框並寫第幾段；並排四個月，更多時橫向捲動。
- 不顯示：在公文系統開啟、填單人、調課單、到職日、請假時段（只剩平常日一種）、出差的公文說明（表單已不用）。

## 簽核流程

- 四張流程表結構相同，以 UNION ALL 一次讀出（[`GdFlowReader`](../../backend/src/AiNexus.Features/Platform/Gd/GdFlowReader.cs)，其他模組可共用）：`FLOW_HISTORY` 已處理、`FLOW_CURRENT` 目前關卡（並簽時多列）、`FLOW_NEXT` 未到。
- 結案後 GD 把歷程搬到 `FLOW_BACKUP_<民國年>`，年取單號第 2～4 碼（`T115100501` → `FLOW_BACKUP_115`）；表不存在或沒有授權時只讀其他三張。
- 核准的單不讀目前與未到的關卡：結案後那兩張表可能留著終結列，當成「還在等」會誤導。
- `STATUS` 照原字顯示（「待處理表單」顯示為「送出」）；`ORIGIN_ID` 是代理簽核的帳號，顯示為「代理：姓名」；`TIMERECORD` 是台灣時間字串。目前關卡另寫已等待幾天。

## 附件

- 清單讀 `Frm_All_attFile`；檔案本身在公文系統網站，網址只在後端組（`Gd:WebBaseUrl` ＋ `servlets/fileservlet?openFile=<attFilePath>/<attNo>`），路徑不合 `/attfile/<年>/<檔名>` 規則的列略過。
- 回應只帶短期憑證，預覽與下載都經 `GET /api/v1/remote-files/content?ticket=…`，機制見 [ADR 0021](../decisions/0021-remote-file-tickets.md)；畫面用共用的檔案清單與閱讀器，PDF 與圖片可預覽，其他種類只能下載。
- 檔案超過 `Gd:MaxFileBytes`（預設 30 MB）回 413，公文系統網站連不上或逾時（`Gd:FileTimeoutSeconds`）回 503 並記日誌 6201。

## 降級

- 公文系統連不上：整張單回 503 `attendance_gd_unavailable`。
- 流程或附件讀不到（多半是少了授權）：該欄位為 null，畫面在那一塊寫「暫時讀不到」，其他內容照常。
