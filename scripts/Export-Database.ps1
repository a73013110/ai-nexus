#requires -Version 7.4

<#
.SYNOPSIS
把開發庫的結構以 SqlPackage Extract 匯出成「每物件一檔」的 SQL，寫入 database/<專案>/，供 SDK-style SQL 專案建出 dacpac。

.DESCRIPTION
DB-first 版控的唯一入口：結構的修改來源是開發庫（DBA 在 SSMS 直接改），repo 的 database/App/ 只放這支腳本的輸出，
沒有人手改。只讀取來源資料庫（Extract 不會修改它）。

匯出內容只有應用程式結構：不含資料、登入、使用者、角色、權限與 EF 的 __EFMigrationsHistory。SqlPackage Extract 沒有
「排除物件類型／排除資料表」參數，所以 IgnorePermissions、IgnoreUserLoginMappings 之外的排除在匯出後刪檔完成；
刪掉的清單會印出來，避免悄悄漏掉物件。

輸出資料夾內既有的 *.sql 會先全部刪除再寫入新匯出（物件被刪除時檔案也要跟著消失），其他檔案（App.sqlproj 等）保留。

.PARAMETER Server
來源 SQL Server，例如 localhost、(localdb)\MSSQLLocalDB 或 sql-dev.company.internal,1433。

.PARAMETER Database
來源資料庫名稱，預設 AiNexus。

.PARAMETER Credential
SQL 登入（需要 VIEW DEFINITION）；省略時以目前 Windows 身分連線。

.PARAMETER TrustServerCertificate
SQL Server 使用自簽憑證時略過憑證鏈驗證；連線仍加密。

.PARAMETER OutputPath
輸出資料夾，預設 repo 的 database/App。

.PARAMETER ExcludeTable
匯出後要移除的資料表（schema.name），預設只有 dbo.__EFMigrationsHistory。

.PARAMETER SkipExtractVerification
不驗證匯出的模型（VerifyExtraction=False）。來源有跨資料庫參照（例如 view 讀取 [Gdweb].dbo.x）時，Extract 解析不到
另一個資料庫而失敗，只能關閉驗證；改由 App.sqlproj 的 ArtifactReference（對方庫的 dacpac）在 build 時驗證。

.PARAMETER SqlPackagePath
SqlPackage 執行檔或 .dll；省略時在 repo 根目錄執行 dotnet tool run sqlpackage（版本由 .config/dotnet-tools.json 固定）。

.EXAMPLE
./scripts/Export-Database.ps1 -Server localhost

.EXAMPLE
./scripts/Export-Database.ps1 -Server '(localdb)\MSSQLLocalDB' -Database AiNexus_PocSource -TrustServerCertificate
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Server,
    [ValidateNotNullOrEmpty()][string]$Database = 'AiNexus',
    [pscredential]$Credential,
    [switch]$TrustServerCertificate,
    [string]$OutputPath,
    [string[]]$ExcludeTable = @('dbo.__EFMigrationsHistory'),
    [switch]$SkipExtractVerification,
    [string]$SqlPackagePath
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$output = if ($OutputPath) { [IO.Path]::GetFullPath($OutputPath, $root) } else { Join-Path $root 'database/App' }

# Extract 參數與原因：
# - ExtractTarget=SchemaObjectType：每物件一檔、依 schema／物件類型分資料夾，diff 與 code review 才看得出改了哪個物件。
# - ExtractAllTableData=False：不帶任何資料（預設值，明寫避免誤改）。
# - ExtractApplicationScopedObjectsOnly=True：只取資料庫內的應用物件，不含登入等伺服器層級物件。
# - ExtractReferencedServerScopedElements=False：使用者參照的登入不要一起帶出。
# - IgnoreUserLoginMappings=True、IgnorePermissions=True：GRANT／使用者對應是環境差異，各環境由 DBA 管理。
# - IgnoreExtendedProperties=False：欄位說明（EF HasComment 產生的 MS_Description）要進版控。
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

$connection = [Data.Common.DbConnectionStringBuilder]::new()
$connection['Server'] = $Server
$connection['Database'] = $Database
$connection['Encrypt'] = 'True'
$connection['TrustServerCertificate'] = [string][bool]$TrustServerCertificate
$connection['Persist Security Info'] = 'False'
# 只讀取；Extract 不需要寫入權限，ApplicationIntent=ReadOnly 讓 AG 讀取副本也能用。
$connection['ApplicationIntent'] = 'ReadOnly'
if ($Credential) {
    $connection['User ID'] = $Credential.UserName
    $connection['Password'] = $Credential.GetNetworkCredential().Password
} else { $connection['Integrated Security'] = 'True' }

# 密碼只寫進目前使用者的暫存資料夾，結束前刪除。先匯出到暫存資料夾，成功後才替換輸出，失敗時不會留下半套檔案。
$work = Join-Path ([IO.Path]::GetTempPath()) ('AiNexus-export-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null
try {
    $response = Join-Path $work 'connection.rsp'
    [IO.File]::WriteAllText($response, (Format-ResponseArgument 'SourceConnectionString' $connection.ConnectionString))
    $connection = $null
    $extracted = Join-Path $work 'extract'
    $watch = [Diagnostics.Stopwatch]::StartNew()
    Invoke-SqlPackage (@('/Action:Extract', "@$response", "/TargetFile:$extracted") + @($properties | ForEach-Object { "/p:$_" }))
    [IO.File]::Delete($response)
    Write-Verbose "Extract 花費 $($watch.Elapsed)"

    # 匯出後排除：EF 的 migration 紀錄表，以及使用者、角色、角色成員（Extract 沒有對應的排除參數）。
    # schema 也放在 Security/，只能依內容判斷，不能整個資料夾刪掉。
    $removed = [Collections.Generic.List[string]]::new()
    foreach ($table in $ExcludeTable) {
        $schema, $name = $table.Split('.', 2)
        if (!$name) { throw "-ExcludeTable 必須是 schema.name：$table" }
        $file = Join-Path $extracted "$schema/Tables/$name.sql"
        if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file; $removed.Add("$schema/Tables/$name.sql") }
    }
    $security = Join-Path $extracted 'Security'
    if (Test-Path -LiteralPath $security) {
        foreach ($file in Get-ChildItem -LiteralPath $security -Filter '*.sql' -Recurse) {
            $text = [IO.File]::ReadAllText($file.FullName)
            if ($text -match '(?im)^\s*CREATE\s+(USER|ROLE|APPLICATION\s+ROLE)\b|^\s*ALTER\s+ROLE\b|sp_addrolemember') {
                Remove-Item -LiteralPath $file.FullName
                $removed.Add([IO.Path]::GetRelativePath($extracted, $file.FullName))
            }
        }
    }
    # 刪除排除物件後留下的空資料夾。
    Get-ChildItem -LiteralPath $extracted -Directory -Recurse | Sort-Object { $_.FullName.Length } -Descending |
        Where-Object { !(Get-ChildItem -LiteralPath $_.FullName -Force) } | Remove-Item

    New-Item -ItemType Directory -Path $output -Force | Out-Null
    # bin/obj 是 dotnet build 的產物，不能誤刪其中的檔案以外的東西；只清掉上一次匯出的 *.sql。
    Get-ChildItem -LiteralPath $output -Filter '*.sql' -Recurse |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } | Remove-Item
    Get-ChildItem -LiteralPath $output -Directory -Recurse | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)([\\/]|$)' } |
        Sort-Object { $_.FullName.Length } -Descending | Where-Object { !(Get-ChildItem -LiteralPath $_.FullName -Force) } | Remove-Item
    Copy-Item -Path (Join-Path $extracted '*') -Destination $output -Recurse -Force

    # SqlPackage 以 UTF-8 BOM 寫檔、行尾 CRLF；統一成 UTF-8 無 BOM，避免 .gitattributes／編輯器造成無意義的 diff。
    foreach ($file in Get-ChildItem -LiteralPath $output -Filter '*.sql' -Recurse | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }) {
        [IO.File]::WriteAllText($file.FullName, [IO.File]::ReadAllText($file.FullName), [Text.UTF8Encoding]::new($false))
    }

    $count = @(Get-ChildItem -LiteralPath $output -Filter '*.sql' -Recurse | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }).Count
    Write-Output "已匯出 $Database 到 $output（$count 個 .sql，花費 $($watch.Elapsed.ToString('mm\:ss'))）。"
    if ($removed.Count) { Write-Output "已排除：$($removed -join '、')" }
} finally {
    Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
}
