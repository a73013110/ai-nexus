#requires -Version 7.4

<#
.SYNOPSIS
把開發庫的結構以 SqlPackage Extract 匯出成「每物件一檔」的 SQL，寫入 database/<專案>/，供 SDK-style SQL 專案建出 dacpac。

.DESCRIPTION
DB-first 版控的唯一入口：結構的修改來源是開發庫（DBA 在 SSMS 直接改），repo 的 database/App/ 只放這支腳本的輸出，
沒有人手改。只讀取來源資料庫（Extract 不會修改它）。

匯出內容只有應用程式結構：不含資料、登入、使用者、角色與權限。SqlPackage Extract 沒有「排除資料表」參數，
也排除不了全文索引，所以匯出後再處理（AiNexus 模組的 Invoke-RepoExtractPostProcess）：
- 移除排除清單（database/App/export-exclusions.txt 與 -ExcludeTable）列出的資料表，以及使用者、角色；移除清單會印出來，
  避免悄悄漏掉物件。不依名稱猜測暫存表或備份表：Orders_temp、temp_Imports 這類名稱也可能是正式的業務表。
- 全文目錄與索引移到 FullText/Catalogs、FullText/Indexes（由發版腳本在有全文元件時執行，不進 dacpac）。
- 主物件之後的索引與欄位說明語句排序，檔案統一為 UTF-8 無 BOM、LF；同一個庫連續匯出兩次必須沒有 diff。

上一次匯出的檔案會先刪除再寫入（物件被刪除時檔案也要跟著消失）；手寫的 FullText/（Catalogs、Indexes 以外）、
Seed/、Data/、bin/obj 與 sqlproj 保留。匯出後比對資料庫的 collation、相容層級、RCSI 與 sqlproj，不一致時只警告：
這些選項不在匯出檔裡，sqlproj 寫錯會讓 dacpac 建出和開發庫不同的庫。

.PARAMETER Server
來源 SQL Server，例如 localhost、(localdb)\MSSQLLocalDB 或 sql-dev.company.internal,1433。

.PARAMETER Database
來源資料庫名稱，預設 AiNexus。

.PARAMETER Credential
SQL 登入（需要 VIEW DEFINITION）；省略時執行中詢問帳密。

.PARAMETER IntegratedSecurity
以目前 Windows 身分連線，不詢問帳密。LocalDB 沒有傳 -Credential 時自動使用 Windows 身分。

.PARAMETER ValidateServerCertificate
驗證 SQL Server 憑證鏈。預設不驗證，因為內部 SQL Server 多半是自簽憑證；連線仍加密。

.PARAMETER OutputPath
輸出資料夾，預設 repo 的 database/App。

.PARAMETER ExclusionFile
排除清單，一行一個 schema.name（可用萬用字元 -like），# 之後是註解；預設 database/App/export-exclusions.txt。
清單裡沒有對應任何資料表的項目會警告，表刪掉後記得移除該行。

.PARAMETER ExcludeTable
這次額外排除的資料表，格式同排除清單，與清單合併。要長期排除的表寫進清單，才會經過 code review。

.PARAMETER SkipExtractVerification
不驗證匯出的模型（VerifyExtraction=False）。來源有跨資料庫參照（例如 view 讀取 [Gdweb].dbo.x）時，Extract 解析不到
另一個資料庫而失敗，只能關閉驗證；改由 App.sqlproj 的 ArtifactReference（對方庫的 dacpac）在 build 時驗證。

.PARAMETER SqlPackagePath
SqlPackage 執行檔或 .dll；省略時在 repo 根目錄執行 dotnet tool run sqlpackage（版本由 .config/dotnet-tools.json 固定）。

.EXAMPLE
./scripts/Export-Database.ps1 -Server sql-dev.company.internal

.EXAMPLE
./scripts/Export-Database.ps1 -Server localhost -IntegratedSecurity

.EXAMPLE
./scripts/Export-Database.ps1 -Server '(localdb)\MSSQLLocalDB' -Database AiNexus_Sandbox -OutputPath .local/export
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Server,
    [ValidateNotNullOrEmpty()][string]$Database = 'AiNexus',
    [pscredential]$Credential,
    [switch]$IntegratedSecurity,
    [switch]$ValidateServerCertificate,
    [string]$OutputPath,
    [string]$ExclusionFile,
    [string[]]$ExcludeTable = @(),
    [switch]$SkipExtractVerification,
    [string]$SqlPackagePath
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'AiNexus') -Force

$root = Get-RepoRoot
$output = if ($OutputPath) { [IO.Path]::GetFullPath($OutputPath, $root) } else { Join-Path $root 'database/App' }
$exclusionPath = if ($ExclusionFile) { [IO.Path]::GetFullPath($ExclusionFile, $root) } else { Join-Path $root 'database/App/export-exclusions.txt' }
if ($ExclusionFile -and !(Test-Path -LiteralPath $exclusionPath -PathType Leaf)) { throw "找不到排除清單：$exclusionPath" }
$exclusions = @(@(Read-RepoExclusionList $exclusionPath) + $ExcludeTable | Select-Object -Unique)

if ($Credential -and $IntegratedSecurity) { throw '-Credential 與 -IntegratedSecurity 只能擇一。' }
# LocalDB 只接受 Windows 身分；其他伺服器預設用 SQL 登入，沒有傳 -Credential 就詢問，密碼不會出現在指令歷史。
$integrated = $IntegratedSecurity -or (!$Credential -and $Server -like '(localdb)*')
if (!$integrated -and !$Credential) {
    $Credential = Get-Credential -Message "登入 $Server 的 SQL 帳號（需要 VIEW DEFINITION）"
    if (!$Credential) { throw '沒有輸入 SQL 帳密；要以 Windows 身分連線請加 -IntegratedSecurity。' }
}

# Extract 參數與原因：
# - ExtractTarget=SchemaObjectType：每物件一檔、依 schema／物件類型分資料夾，diff 與 code review 才看得出改了哪個物件。
# - ExtractAllTableData=False：不帶任何資料（預設值，明寫避免誤改）。
# - ExtractApplicationScopedObjectsOnly=True：只取資料庫內的應用物件，不含登入等伺服器層級物件。
# - ExtractReferencedServerScopedElements=False：使用者參照的登入不要一起帶出。
# - IgnoreUserLoginMappings=True、IgnorePermissions=True：GRANT／使用者對應是環境差異，各環境由 DBA 管理。
# - IgnoreExtendedProperties=False：欄位說明（MS_Description）要進版控。
# - VerifyExtraction=True：匯出後驗證模型可以編譯，壞掉的物件（例如參照不存在的表）在這裡就會失敗；跨庫參照見 -SkipExtractVerification。
$properties = @(
    'ExtractTarget=SchemaObjectType', 'ExtractAllTableData=False', 'ExtractApplicationScopedObjectsOnly=True',
    'ExtractReferencedServerScopedElements=False', 'IgnoreUserLoginMappings=True', 'IgnorePermissions=True',
    'IgnoreExtendedProperties=False', "VerifyExtraction=$(!$SkipExtractVerification)")

# SqlPackage 在 response file 中把 \" 當引號，且解析失敗會把整個參數（含密碼）印出來；做法同後端 SqlPackage.ResponseFileArgument。
function Format-ResponseArgument([string]$Name, [string]$Value) {
    if ($Value -match '[\r\n]') { throw "$Name 不能含換行。" }
    if ($Value.EndsWith('\')) { $Value += ';' }
    "/$($Name):`"$($Value.Replace('"', '\"'))`""
}

function Invoke-SqlPackage([string[]]$Arguments) {
    if ($SqlPackagePath) {
        if ($SqlPackagePath -like '*.dll') { & dotnet $SqlPackagePath @Arguments | Out-Host } else { & $SqlPackagePath @Arguments | Out-Host }
    } else {
        Push-Location $root
        try { & dotnet tool run sqlpackage -- @Arguments | Out-Host } finally { Pop-Location }
    }
    if ($LASTEXITCODE -ne 0) { throw "SqlPackage Extract 失敗（結束碼 $LASTEXITCODE）；原因見上方輸出。找不到工具時先在 repo 根目錄執行 dotnet tool restore。" }
}

# 資料庫層級選項不在每物件一檔的匯出裡，另外查詢。用 PowerShell 內附的 System.Data.SqlClient，省得另外安裝模組；
# 它不支援 LocalDB（shared memory）的加密，所以只有 LocalDB 不要求加密。讀不到只警告，不影響匯出。
function Get-DatabaseSetting([string]$ConnectionString) {
    $builder = [Data.SqlClient.SqlConnectionStringBuilder]::new($ConnectionString)
    if ($Server -like '(localdb)*') { $builder.Encrypt = $false }
    $connection = [Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
    try {
        $connection.Open()
        $command = $connection.CreateCommand()
        $command.CommandText = 'SELECT collation_name, compatibility_level, is_read_committed_snapshot_on FROM sys.databases WHERE database_id = DB_ID();'
        $reader = $command.ExecuteReader()
        if (!$reader.Read()) { throw '查不到目前資料庫。' }
        @{ Collation = $reader.GetString(0); CompatibilityLevel = [int]$reader.GetByte(1); ReadCommittedSnapshot = $reader.GetBoolean(2) }
    } finally { $connection.Dispose() }
}

$connection = [Data.Common.DbConnectionStringBuilder]::new()
$connection['Server'] = $Server
$connection['Database'] = $Database
$connection['Encrypt'] = 'True'
$connection['TrustServerCertificate'] = [string](!$ValidateServerCertificate)
$connection['Persist Security Info'] = 'False'
# 只讀取；Extract 不需要寫入權限，ApplicationIntent=ReadOnly 讓 AG 讀取副本也能用。
$connection['ApplicationIntent'] = 'ReadOnly'
if (!$integrated) {
    $connection['User ID'] = $Credential.UserName
    $connection['Password'] = $Credential.GetNetworkCredential().Password
} else { $connection['Integrated Security'] = 'True' }

# 密碼只寫進目前使用者的暫存資料夾，結束前刪除。先匯出到暫存資料夾，成功後才替換輸出，失敗時不會留下半套檔案。
$work = Join-Path ([IO.Path]::GetTempPath()) ('AiNexus-export-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null
try {
    $warnings = [Collections.Generic.List[string]]::new()
    try { $databaseSettings = Get-DatabaseSetting $connection.ConnectionString }
    catch { $databaseSettings = $null; $warnings.Add("讀不到資料庫設定，未比對 sqlproj：$($_.Exception.Message)") }

    $response = Join-Path $work 'connection.rsp'
    [IO.File]::WriteAllText($response, (Format-ResponseArgument 'SourceConnectionString' $connection.ConnectionString))
    $connection = $null
    $extracted = Join-Path $work 'extract'
    $watch = [Diagnostics.Stopwatch]::StartNew()
    Invoke-SqlPackage (@('/Action:Extract', "@$response", "/TargetFile:$extracted") + @($properties | ForEach-Object { "/p:$_" }))
    [IO.File]::Delete($response)
    # SqlPackage 連線失敗時結束碼仍可能是 0；沒有產出就停在這裡，否則下一步會用空資料夾取代整份匯出。
    if (!(Test-Path -LiteralPath $extracted -PathType Container) -or !(Get-ChildItem -LiteralPath $extracted -Filter '*.sql' -Recurse | Select-Object -First 1)) {
        throw 'SqlPackage Extract 沒有產生任何檔案，未更動輸出資料夾；原因見上方輸出（例如帳密錯誤、伺服器無法連線，或加了 -ValidateServerCertificate 但憑證不受信任）。'
    }
    Write-Verbose "Extract 花費 $($watch.Elapsed)"

    $removed = Invoke-RepoExtractPostProcess $extracted $exclusions
    foreach ($pattern in @(Get-RepoUnusedExclusion $exclusions $removed)) { $warnings.Add("排除清單的 $pattern 沒有對應任何資料表；表已刪除就從 $exclusionPath 移除這一行。") }
    Update-RepoDatabaseProject $extracted $output

    $count = @(Get-ChildItem -LiteralPath $extracted -Filter '*.sql' -Recurse).Count
    Write-Output "已匯出 $Database 到 $output（$count 個 .sql，花費 $($watch.Elapsed.ToString('mm\:ss'))）。"
    if ($removed.Count) { Write-Output "已排除：$($removed -join '、')" }

    $project = @(Get-ChildItem -LiteralPath $output -Filter '*.sqlproj')
    if ($project.Count -ne 1) { $warnings.Add("$output 應該剛好有一個 .sqlproj，找到 $($project.Count) 個；未比對資料庫設定。") }
    elseif ($databaseSettings) { foreach ($message in Compare-RepoDatabaseSetting $databaseSettings (Get-RepoSqlProjectSetting $project[0].FullName)) { $warnings.Add($message) } }
    foreach ($message in $warnings) { Write-Warning $message }
} finally {
    Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
}
