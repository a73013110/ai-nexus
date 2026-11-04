# 打卡機主機排程（HAMS）

結論：刷卡先存在打卡機（環球 RAC-960PE），由同機的 HAMS 接收進 `HAMS_{yyyy}.mdb`，再轉出到 `C:\Export\yyyyMMdd.TXT`；本系統只讀這個資料夾（[刷卡匯入](../features/attendance-imports.md)），不讀 HAMS 的資料庫、也不呼叫 HAMS（原因見 [ADR 0018](../decisions/0018-attendance-cards-and-punches.md#不採用)）。中午多跑一次「接收＋轉出」，白天就看得到當天幾點到。

## HAMS 的指令

`C:\Hundure\HAMS-19\HAMS.exe` 帶一個參數執行，做完就結束：

| 參數 | 做什麼 |
| --- | --- |
| `AUTO2` | 校正打卡機時鐘 |
| `AUTO5` | 接收：把打卡機上的刷卡寫進 `HAMS_{yyyy}.mdb`；已收過的會因唯一索引略過，記在 `Event\RetryWriteSwip.log` |
| `AUTO4` | 轉出：還沒轉出過的刷卡（近 8 天內），每張卡留這一批的最早與最晚一筆，**附加**到執行當天的檔，再標記為已轉出 |

- 每筆刷卡只轉出一次（`PubEvent.TransMark`），所以檔案只會變長：一天跑幾次，同一張卡就可能有幾組首末筆。檔案最多是當天的全部刷卡，判定只取最早與最晚。
- 檔名是**執行當天**：前一晚 23:37 以後的刷卡，會在隔天中午寫進隔天的檔；出勤日依刷卡時間，不受影響。
- 沒有新刷卡（假日）就不寫檔，`ScheduleLog` 只記「轉出完成」。只跑 `AUTO4` 也一樣：刷卡還在打卡機上。
- 只留首末筆由 `Config.xml` 的 `FisrtLastbyCard=Y` 決定；回看天數是 `ScheduleShiftDay=7`。範本（`ScheduleExport`）的第一欄與最後一欄是固定字 `001`、`11`。
- `AUTO1` 也是增量轉出，但每一筆都轉、依刷卡日期分檔；本系統兩種都能匯入，不必改。

## 排程

| 工作 | 時間 | 內容 |
| --- | --- | --- |
| `HAMS_PullNow.bat` | 每天 11:52 | `AUTO5` 接收、`AUTO4` 轉出；不校時、不複製 |
| `HAMS_Auto.bat` | 每天 23:37 | `AUTO2` 校時、`AUTO5` 接收 |
| `HAMS_Export.bat` | 每天 23:52 | `AUTO4` 轉出，再以 `xcopy` 複製到網路共用與備份 |

- 每個 bat 都先 `taskkill /f /im HAMS.exe`，有人開著 HAMS 畫面會被關掉；三個時間要錯開，兩個 HAMS 同時跑會互相關掉對方。
- 以和每晚排程相同的帳號執行，HAMS 寫入資料庫的權限才一致；複製到網路共用的 bat 不要寫入帳密，改用有權限的排程帳號執行。
- 轉出後本系統在一個掃描間隔內匯入（預設 5 分鐘），只新增、不刪除。

`HAMS_PullNow.bat`（放在 `C:\Hundure\HAMS-19\`；要加中文註解時以 Big5 存檔，與 HAMS 附的 bat 一致）：

```bat
@echo off
set HAMS=C:\Hundure\HAMS-19
set LOG=%HAMS%\Logs\HAMS_PullNow.log
cd /d "%HAMS%"
echo %date% %time% start>>"%LOG%"
taskkill /f /im HAMS.exe >nul 2>&1
start "" /wait "%HAMS%\HAMS.exe" AUTO5
echo %date% %time% AUTO5 exit=%errorlevel%>>"%LOG%"
start "" /wait "%HAMS%\HAMS.exe" AUTO4
echo %date% %time% AUTO4 exit=%errorlevel%>>"%LOG%"
exit /b 0
```

## 不要刪除或修改 `C:\Export` 的檔

已轉出的刷卡不會再轉出：刪掉檔案再跑 `AUTO4`，新檔只有之後的刷卡。本系統已匯入的不受影響，但檔案與備份就少了。

補回某天的檔：登入 HAMS，開「首末筆轉出」，選那天按「轉出」。它不看也不改轉出標記，把整天的首末筆附加到 `C:\Export\刷卡日期.TXT`，與檔內原有的行重複也沒關係。要讓本系統的資料與修正後的檔一致（例如拿掉誤刷），在匯入紀錄頁手動重匯那天：手動重匯會整檔取代。

## 本系統的設定

- 一般設定 `Attendance.Import.SourceDirectory` 設為 `C:\Export\`（見 [IIS 外部設定](iis-configuration.md)），改完回收應用程式集區。
- 集區身分要能讀取：`icacls C:\Export /grant "IIS AppPool\<集區名>:(OI)(CI)RX"`。
- 匯入紀錄頁看得到 `yyyyMMdd.TXT` 的排程匯入即生效；沒有出現時先看 `C:\Export\` 有沒有當天的檔，再看 HAMS 的 `ScheduleLog` 與 `Logs\HAMS_PullNow.log`。
