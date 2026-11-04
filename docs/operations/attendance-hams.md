# 打卡機主機排程（HAMS）

結論：刷卡先存在打卡機（環球 RAC-960PE），由同機的 HAMS 接收進 `HAMS_{yyyy}.mdb`，再轉出當天的 `C:\Export\yyyyMMdd.TXT`；本系統只讀這個資料夾（[刷卡匯入](../features/attendance-imports.md)），不讀 HAMS 的資料庫、也不呼叫 HAMS（原因見 [ADR 0018](../decisions/0018-attendance-cards-and-punches.md#不採用)）。要讓白天看得到當天的刷卡，就在工作排程器多跑一次「接收＋轉出」。

## HAMS 的指令

`C:\Hundure\HAMS-19\HAMS.exe` 帶一個參數執行，做完就結束：

| 參數 | 做什麼 |
| --- | --- |
| `AUTO2` | 校正打卡機時鐘 |
| `AUTO5` | 接收：把打卡機上的刷卡寫進 `HAMS_{yyyy}.mdb`；已收過的會因唯一索引略過，記在 `Event\RetryWriteSwip.log` |
| `AUTO4` | 轉出：把**當天**的刷卡寫成 `C:\Export\yyyyMMdd.TXT`，每張卡只有最早與最晚一筆，整檔覆蓋 |

- 只跑 `AUTO4` 不會有新資料：當天的刷卡還在打卡機上。沒有刷卡的日子（假日）不產生檔案，`ScheduleLog` 只記「轉出完成」。
- 只留最早與最晚由 `Config.xml` 的 `FisrtLastbyCard=Y` 決定；出勤判定只用這兩筆，不必改。
- 轉出格式由 HAMS 的 `ScheduleExport` 範本組成，第一欄與最後一欄是固定字 `001`、`11`。

## 排程

| 工作 | 時間 | 內容 |
| --- | --- | --- |
| `HAMS_Auto.bat` | 每晚（目前約 23:37） | `AUTO2` 校時、`AUTO5` 接收 |
| `HAMS_Export.bat` | 每晚 23:52 | `AUTO4` 轉出，再以 `xcopy` 複製到網路共用與備份 |
| `HAMS_PullNow.bat` | 白天（例如 12:30），可手動執行 | `AUTO5` 接收、`AUTO4` 轉出；不校時、不複製 |

- 每個 bat 都先 `taskkill /f /im HAMS.exe`，有人開著 HAMS 畫面會被關掉；白天選少人使用的時段，並避開每晚的 23:30～23:55，兩個 HAMS 同時跑會互相關掉對方。
- 以和每晚排程相同的帳號執行，HAMS 寫入資料庫的權限才一致。
- 轉出後本系統在一個掃描間隔內重匯（預設 5 分鐘）；23:52 那次是當天的完整版本，必須保留。
- 複製到網路共用的 bat 不要寫入帳密，改用有權限的排程帳號執行。

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

## 本系統的設定

- 一般設定 `Attendance.Import.SourceDirectory` 設為 `C:\Export\`（見 [IIS 外部設定](iis-configuration.md)），改完回收應用程式集區。
- 集區身分要能讀取：`icacls C:\Export /grant "IIS AppPool\<集區名>:(OI)(CI)RX"`。
- 匯入紀錄頁看得到 `yyyyMMdd.TXT` 的排程匯入即生效；沒有出現時先看 `C:\Export\` 有沒有當天的檔，再看 HAMS 的 `ScheduleLog` 與 `Logs\HAMS_PullNow.log`。
