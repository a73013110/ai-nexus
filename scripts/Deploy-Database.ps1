#requires -Version 7.4

<#
.SYNOPSIS
以 App.dacpac 部署資料庫結構，並依序執行一次性資料修正、全文物件與種子資料。

.DESCRIPTION
發版順序（ADR 0010）：Data/<版本>/pre-*.sql → 結構 → FullText/（先 Catalogs/ 再 Indexes/）→ Data/<版本>/post-*.sql → Seed/。
-Action 決定做哪一段：
- Script（預設，唯讀）：以 SqlPackage 產生差異報告 DeployReport.xml 與升級 SQL Upgrade.sql，交給 DBA 審核；列出 Pre、Post 會執行的腳本。
- Pre：執行 Data/<版本>/pre-*.sql。在 DBA 執行 Upgrade.sql 之前。
- Post：執行 FullText/、Data/<版本>/post-*.sql 與 Seed/。在 DBA 執行 Upgrade.sql 之後。
- Publish：Pre → SqlPackage 直接套用結構 → Post。只用於開發機與測試環境；正式環境走 Script → 審核 → Pre → Upgrade.sql → Post。
部署後以 Verify-Database.ps1 比對實際結構。

目標庫不存在時建立它，並套用 dacpac 的 collation、相容層級與 RCSI（App.sqlproj）；既有庫不改資料庫層級選項
（RCSI 的切換需要獨佔，見 docs/operations/database-setup.md）。
全文物件：偵測不到全文元件時跳過；匯出的語句沒有存在檢查，所以已存在的目錄、停用字表與索引整檔略過，定義變更由 DBA 手動處理。
種子資料只新增缺少的列，可重複執行。手寫腳本以 GO 分批、不支援 SQLCMD 指令。
隨發布套件複製到主機（與 database\ 放在一起），不依賴 repo；需要 SqlPackage，找不到時會說明安裝方式。

.PARAMETER Server
目標 SQL Server，例如 sql.company.internal、sql.company.internal,1433 或 (localdb)\MSSQLLocalDB。

.PARAMETER Action
Script（預設）、Pre、Post 或 Publish，見說明。

.PARAMETER Database
目標資料庫，預設 AiNexus。

.PARAMETER Release
這次發版的版本，對應 Data/<版本>/ 的資料夾；省略時不執行 Data/ 的腳本。

.PARAMETER Path
含 App.dacpac 與 FullText/、Seed/、Data/ 的資料夾。省略時用發布套件的 database\；在 repo 內執行時用 database/App
（dacpac 取 bin/Release，先 dotnet build database/App -c Release）。

.PARAMETER OutputPath
Script 寫出報告與升級 SQL 的資料夾，預設目前目錄下的 <資料庫>-<UTC 時間>；Publish 指定時另外保留實際套用的報告與 SQL。

.PARAMETER AllowDataLoss
不因可能遺失資料（刪欄位、縮短長度等）而中止。只在 DBA 審核過這次的報告後單次使用。

.PARAMETER Credential
SQL 登入（部署帳號：結構變更需要 db_ddladmin 或 db_owner，建庫需要 CREATE DATABASE）；省略時以目前 Windows 身分連線。

.PARAMETER TrustServerCertificate
SQL Server 使用自簽憑證時略過憑證鏈驗證；連線仍加密。

.PARAMETER SqlPackagePath
SqlPackage 執行檔或 .dll；省略時依序找 PATH 上的 sqlpackage、DacFx 的預設安裝位置，以及 repo 的 dotnet 工具。

.COMPONENT
IIS 主機

.EXAMPLE
./Deploy-Database.ps1 -Server sql.company.internal -Release 2026.11.0

.EXAMPLE
./Deploy-Database.ps1 -Server sql.company.internal -Release 2026.11.0 -Action Post -Credential (Get-Credential)

.EXAMPLE
./scripts/Deploy-Database.ps1 -Server '(localdb)\MSSQLLocalDB' -Database AiNexus_Sandbox -Action Publish
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Server,
    [ValidateSet('Script', 'Pre', 'Post', 'Publish')][string]$Action = 'Script',
    [ValidateNotNullOrEmpty()][string]$Database = 'AiNexus',
    [ValidatePattern('^[\w.-]+$')][string]$Release,
    [string]$Path,
    [string]$OutputPath,
    [switch]$AllowDataLoss,
    [pscredential]$Credential,
    [switch]$TrustServerCertificate,
    [string]$SqlPackagePath
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# 與 Verify-Database.ps1、.config/dotnet-tools.json 相同（scripts/tests 檢查）。
$sqlPackageVersion = '170.5.96'

# 權限、登入、檔案位置是環境差異，由 DBA 管理；全文物件不在 dacpac，由 FullText/ 另外執行。
$excluded = @(
    'Users', 'Logins', 'DatabaseRoles', 'ApplicationRoles', 'RoleMembership', 'Permissions', 'Credentials', 'DatabaseScopedCredentials',
    'ServerRoles', 'ServerRoleMembership', 'ServerTriggers', 'LinkedServers', 'LinkedServerLogins', 'Endpoints', 'ErrorMessages',
    'Audits', 'ServerAuditSpecifications', 'DatabaseAuditSpecifications', 'EventSessions', 'Certificates', 'MasterKeys',
    'DatabaseEncryptionKeys', 'CryptographicProviders', 'Filegroups', 'Files', 'FullTextCatalogs', 'FullTextStoplists')

function Resolve-DatabasePath {
    if ($Path) { $folder = [IO.Path]::GetFullPath($Path) }
    elseif (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'database/App.dacpac')) { $folder = Join-Path $PSScriptRoot 'database' }
    else { $folder = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../database/App')) }
    $dacpac = @((Join-Path $folder 'App.dacpac'), (Join-Path $folder 'bin/Release/App.dacpac')) | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
    if (!$dacpac) { throw "在 $folder 找不到 App.dacpac。發布套件的在 database\；repo 內先執行 dotnet build database/App -c Release。" }
    @{ Folder = $folder; Dacpac = $dacpac }
}

function Find-SqlPackage {
    if ($SqlPackagePath) {
        if (!(Test-Path -LiteralPath $SqlPackagePath -PathType Leaf)) { throw "找不到 -SqlPackagePath：$SqlPackagePath" }
        return @{ Path = (Resolve-Path -LiteralPath $SqlPackagePath).Path }
    }
    $command = Get-Command sqlpackage -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($command) { return @{ Path = $command.Source } }
    if ($env:ProgramFiles) {
        foreach ($version in '170', '160') {
            $candidate = Join-Path $env:ProgramFiles "Microsoft SQL Server/$version/DAC/bin/SqlPackage.exe"
            if (Test-Path -LiteralPath $candidate -PathType Leaf) { return @{ Path = $candidate } }
        }
    }
    # 在 repo 內執行（開發機）時用 Restore.ps1 還原的本機工具。
    $repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
    if ((Test-Path -LiteralPath (Join-Path $repository '.config/dotnet-tools.json')) -and (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        return @{ Tool = $repository }
    }
    throw @"
找不到 SqlPackage，無法部署資料庫。請擇一安裝後重試，或以 -SqlPackagePath 指定 SqlPackage.exe：
- 有 .NET SDK 的機器：dotnet tool install --global microsoft.sqlpackage --version $sqlPackageVersion（重開終端機後 sqlpackage 在 PATH 上）
- 只有 Hosting Bundle 的 IIS 主機：從 https://learn.microsoft.com/sql/tools/sqlpackage/sqlpackage-download 下載 Windows 版 zip，解壓後以 -SqlPackagePath 指定其中的 SqlPackage.exe
- 也可以在能連到目標 SQL 的 DBA 工作站執行本腳本
"@
}

# SqlPackage 逐一列出每個建立的物件；成功時只在 -Verbose 顯示，失敗時全部印出，原因通常在最後幾行。
function Invoke-SqlPackage([hashtable]$SqlPackage, [string[]]$Arguments) {
    $output = if ($SqlPackage.Contains('Tool')) {
        Push-Location -LiteralPath $SqlPackage.Tool
        try { & dotnet tool run sqlpackage -- @Arguments 2>&1 } finally { Pop-Location }
    } elseif ($SqlPackage.Path -like '*.dll') { & dotnet $SqlPackage.Path @Arguments 2>&1 }
    else { & $SqlPackage.Path @Arguments 2>&1 }
    if ($LASTEXITCODE -ne 0) {
        $output | Out-Host
        throw "SqlPackage 失敗（結束碼 $LASTEXITCODE）；原因見上方輸出（連線、權限、可能遺失資料）。"
    }
    $output | ForEach-Object { Write-Verbose "$_" }
}

# SqlPackage 在 response file 中把 \" 當成引號，無法解析的引數會原樣印出；所以連線字串要小心加引號，且結尾不可是反斜線。
function Format-ResponseArgument([string]$Name, [string]$Value) {
    if ($Value -match '[\r\n]') { throw "$Name 不能含換行。" }
    if ($Value.EndsWith('\')) { $Value += ';' }
    "/$($Name):`"$($Value.Replace('"', '\"'))`""
}

function New-ConnectionString([string]$Catalog) {
    $connection = [Data.Common.DbConnectionStringBuilder]::new()
    $connection['Server'] = $Server
    $connection['Database'] = $Catalog
    $connection['Encrypt'] = 'True'
    $connection['TrustServerCertificate'] = [string][bool]$TrustServerCertificate
    $connection['Persist Security Info'] = 'False'
    if ($Credential) {
        $connection['User ID'] = $Credential.UserName
        $connection['Password'] = $Credential.GetNetworkCredential().Password
    } else { $connection['Integrated Security'] = 'True' }
    $connection.ConnectionString
}

# 手寫腳本用 PowerShell 內附的 System.Data.SqlClient 執行，主機不必另外安裝 sqlcmd。它不支援 LocalDB（shared memory）的加密，
# 所以只有 LocalDB 不要求加密；密碼以 SqlCredential 傳入，不放進連線字串。
function Open-SqlConnection([string]$Catalog) {
    # 連線字串產生器是字典，PowerShell 對它的屬性指派會變成加鍵，所以一律用索引子。
    $builder = [Data.SqlClient.SqlConnectionStringBuilder]::new()
    $builder['Data Source'] = $Server
    $builder['Initial Catalog'] = $Catalog
    $builder['Encrypt'] = $Server -notlike '(localdb)*'
    $builder['TrustServerCertificate'] = [bool]$TrustServerCertificate
    $builder['Application Name'] = 'AiNexus Deploy-Database'
    $credential = $null
    if ($Credential) {
        $password = $Credential.Password.Copy()
        $password.MakeReadOnly()
        $credential = [Data.SqlClient.SqlCredential]::new($Credential.UserName, $password)
    } else { $builder['Integrated Security'] = $true }
    $connection = [Data.SqlClient.SqlConnection]::new($builder.ConnectionString, $credential)
    $connection.add_InfoMessage({ param($source, $info) Write-Output "  $($info.Message)" | Out-Host })
    $connection.Open()
    $connection
}

function Invoke-Scalar($Connection, [string]$Sql, [hashtable]$Parameters = @{}) {
    $command = $Connection.CreateCommand()
    $command.CommandText = $Sql
    foreach ($name in $Parameters.Keys) { [void]$command.Parameters.AddWithValue("@$name", $Parameters[$name]) }
    $command.ExecuteScalar()
}

function Test-DatabaseExists {
    $master = Open-SqlConnection 'master'
    try { (Invoke-Scalar $master 'SELECT DB_ID(@Name)' @{ Name = $Database }) -isnot [DBNull] } finally { $master.Dispose() }
}

function Get-SqlBatch([string]$File) {
    $text = [IO.File]::ReadAllText($File)
    if ($text -match '(?im)^\s*:(setvar|on|r|connect)\b') { throw "$File 含 SQLCMD 指令；手寫腳本只能用 T-SQL 與 GO。" }
    @([regex]::Split($text, '(?im)^\s*GO\s*$') | Where-Object { $_.Trim() })
}

function Invoke-SqlFile($Connection, [string]$File) {
    Write-Output "執行 $([IO.Path]::GetRelativePath($layout.Folder, $File))"
    foreach ($batch in Get-SqlBatch $File) {
        $command = $Connection.CreateCommand()
        $command.CommandText = $batch
        $command.CommandTimeout = 0
        try { [void]$command.ExecuteNonQuery() }
        catch { throw "執行 $File 失敗：$($_.Exception.InnerException?.Message ?? $_.Exception.Message)" }
    }
}

function Get-SqlFile([string]$Folder, [string]$Filter = '*.sql') {
    if (!(Test-Path -LiteralPath $Folder -PathType Container)) { return @() }
    @(Get-ChildItem -LiteralPath $Folder -Filter $Filter -File | Sort-Object { $_.Name } -Culture ([cultureinfo]::InvariantCulture) | ForEach-Object FullName)
}

function Get-DataScript([string]$Prefix) {
    if (!$Release) { return @() }
    Get-SqlFile (Join-Path $layout.Folder "Data/$Release") "$Prefix-*.sql"
}

# 匯出的全文語句是 CREATE，沒有存在檢查；依檔案第一個 CREATE 的物件判斷，已存在就整檔略過（同檔的 ALTER 也是同一個物件）。
function Test-FullTextObjectExists($Connection, [string]$File) {
    $name = '(?:\[(?:[^\]]|\]\])+\]|[\w@#$]+)'
    $create = Get-SqlBatch $File | Where-Object { $_ -match '^\s*CREATE\b' } | Select-Object -First 1
    if (!$create) { throw "$File 沒有 CREATE 語句，無法判斷全文物件是否已存在。" }
    $unquote = { param($value) if ($value.StartsWith('[')) { $value.Substring(1, $value.Length - 2).Replace(']]', ']') } else { $value } }
    if ($create -match "^\s*CREATE\s+FULLTEXT\s+INDEX\s+ON\s+($name\.$name)") {
        return [int](Invoke-Scalar $Connection 'SELECT COUNT(*) FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID(@Name)' @{ Name = $Matches[1] }) -gt 0
    }
    $catalogs = @{ 'FULLTEXT CATALOG' = 'sys.fulltext_catalogs'; 'FULLTEXT STOPLIST' = 'sys.fulltext_stoplists'; 'SEARCH PROPERTY LIST' = 'sys.registered_search_property_lists' }
    if ($create -match "^\s*CREATE\s+(FULLTEXT\s+CATALOG|FULLTEXT\s+STOPLIST|SEARCH\s+PROPERTY\s+LIST)\s+($name)") {
        $view = $catalogs[($Matches[1] -replace '\s+', ' ').ToUpperInvariant()]
        return [int](Invoke-Scalar $Connection "SELECT COUNT(*) FROM $view WHERE name = @Name" @{ Name = (& $unquote $Matches[2]) }) -gt 0
    }
    throw "$File 不是可辨識的全文物件（目錄、停用字表、搜尋屬性清單或全文索引）。"
}

function Invoke-Pre {
    $scripts = @(Get-DataScript 'pre')
    if (!$scripts.Count) { Write-Output 'Pre：沒有要執行的 pre-*.sql。'; return }
    $connection = Open-SqlConnection $Database
    try { foreach ($script in $scripts) { Invoke-SqlFile $connection $script } } finally { $connection.Dispose() }
}

function Invoke-Post {
    $connection = Open-SqlConnection $Database
    try {
        $fullText = @(Get-SqlFile (Join-Path $layout.Folder 'FullText/Catalogs')) + @(Get-SqlFile (Join-Path $layout.Folder 'FullText/Indexes'))
        if ($fullText.Count) {
            if ([int](Invoke-Scalar $connection "SELECT CONVERT(int, FULLTEXTSERVICEPROPERTY('IsFullTextInstalled'))") -ne 1) {
                Write-Warning "$Server 沒有全文元件，略過 FullText/ 的 $($fullText.Count) 個腳本；混合檢索會改用 vector 模式。"
            } else {
                foreach ($script in $fullText) {
                    if (Test-FullTextObjectExists $connection $script) { Write-Output "略過 $([IO.Path]::GetRelativePath($layout.Folder, $script))：已存在（定義變更由 DBA 手動處理）" }
                    else { Invoke-SqlFile $connection $script }
                }
            }
        }
        foreach ($script in @(Get-DataScript 'post') + @(Get-SqlFile (Join-Path $layout.Folder 'Seed'))) { Invoke-SqlFile $connection $script }
    } finally { $connection.Dispose() }
}

function Get-DeployProperty([bool]$NewDatabase) {
    @(
        "BlockOnPossibleDataLoss=$(!$AllowDataLoss)", 'DropObjectsNotInSource=False', 'GenerateSmartDefaults=False', 'IgnoreColumnOrder=True',
        "ExcludeObjectTypes=$($excluded -join ';')", 'IgnorePermissions=True', 'IgnoreRoleMembership=True', 'IgnoreLoginSids=True',
        'IgnoreAuthorizer=True', 'IgnoreUserSettingsObjects=True', 'IgnoreFilegroupPlacement=True', 'IgnoreFileAndLogFilePath=True',
        'IgnoreFileSize=True', 'IncludeTransactionalScripts=True',
        # 新庫才套用 App.sqlproj 的 collation、相容層級與 RCSI；既有庫的資料庫層級選項由 DBA 管理。
        "ScriptDatabaseOptions=$NewDatabase", "ScriptDatabaseCollation=$NewDatabase", "ScriptDatabaseCompatibility=$NewDatabase"
    ) | ForEach-Object { "/p:$_" }
}

function Write-ReportSummary([string]$Report) {
    [xml]$xml = [IO.File]::ReadAllText($Report)
    $namespace = [Xml.XmlNamespaceManager]::new($xml.NameTable)
    $namespace.AddNamespace('r', $xml.DocumentElement.NamespaceURI)
    $items = @(foreach ($operation in $xml.SelectNodes('//r:Operations/r:Operation', $namespace)) {
        foreach ($item in $operation.SelectNodes('r:Item', $namespace)) {
            [pscustomobject]@{ Operation = $operation.GetAttribute('Name'); Type = $item.GetAttribute('Type'); Object = $item.GetAttribute('Value') }
        }
    })
    $alerts = @(foreach ($alert in $xml.SelectNodes('//r:Alerts/r:Alert', $namespace)) {
        foreach ($issue in $alert.SelectNodes('r:Issue', $namespace)) { [pscustomobject]@{ Alert = $alert.GetAttribute('Name'); Detail = $issue.GetAttribute('Value') } }
    })
    if (!$items.Count) { Write-Output '結構與 App.dacpac 相同，沒有要套用的變更。' }
    else {
        Write-Output "結構變更 $($items.Count) 項："
        # 建新庫時會列出每個物件；太多時只列各類數量，完整清單在 DeployReport.xml。
        $rows = if ($items.Count -le 40) { $items } else { $items | Group-Object Operation, Type | ForEach-Object { [pscustomobject]@{ Operation = $_.Group[0].Operation; Type = $_.Group[0].Type; Count = $_.Count } } }
        $rows | Format-Table -AutoSize | Out-String -Width 200 | ForEach-Object TrimEnd | Write-Output
    }
    if ($alerts.Count) {
        Write-Output '警示（DataIssue＝可能遺失資料，預設會中止）：'
        $alerts | Format-Table -AutoSize -Wrap | Out-String -Width 200 | ForEach-Object TrimEnd | Write-Output
    }
}

$layout = Resolve-DatabasePath
# 先找工具再連線：缺工具時立刻說明安裝方式，不必等連線逾時。
$sqlPackage = if ($Action -in 'Script', 'Publish') { Find-SqlPackage }
$data = Join-Path $layout.Folder 'Data'
if (!$Release) {
    $versions = @(if (Test-Path -LiteralPath $data -PathType Container) { Get-ChildItem -LiteralPath $data -Directory | ForEach-Object Name })
    if ($versions.Count) { Write-Warning "未指定 -Release，不執行 Data/ 的一次性腳本（有：$($versions -join '、')）。" }
} elseif (!(Test-Path -LiteralPath (Join-Path $data $Release) -PathType Container)) {
    Write-Output "Data/$Release 不存在：這一版沒有一次性資料修正。"
}

$exists = Test-DatabaseExists
if ($Action -in 'Pre', 'Post' -and !$exists) { throw "$Server 上沒有 $Database；先以 -Action Script 產生建庫 SQL（或開發環境用 -Action Publish）。" }

if ($Action -in 'Script', 'Publish') {
    $output = if ($OutputPath) { [IO.Path]::GetFullPath($OutputPath) }
        elseif ($Action -eq 'Script') { Join-Path (Get-Location) "$Database-$([DateTime]::UtcNow.ToString('yyyyMMddTHHmmss'))" }
    if ($output) { New-Item -ItemType Directory -Path $output -Force | Out-Null }
    # 密碼只寫進目前使用者的暫存資料夾，結束前刪除。
    $work = Join-Path ([IO.Path]::GetTempPath()) ('AiNexus-deploy-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $work | Out-Null
    try {
        $response = Join-Path $work 'connection.rsp'
        [IO.File]::WriteAllText($response, (Format-ResponseArgument 'TargetConnectionString' (New-ConnectionString $Database)))
        if (!$exists) { Write-Output "$Server 上沒有 $Database：會建立新庫，並套用 App.dacpac 的 collation、相容層級與 RCSI。" }
        if ($Action -eq 'Script') {
            $report = Join-Path $output 'DeployReport.xml'
            $upgrade = Join-Path $output 'Upgrade.sql'
            Invoke-SqlPackage $sqlPackage (@('/Action:Script', "/SourceFile:$($layout.Dacpac)", "@$response", "/DeployReportPath:$report", "/DeployScriptPath:$upgrade") + (Get-DeployProperty (!$exists)))
            Write-ReportSummary $report
            $pre = @(Get-DataScript 'pre')
            $post = @(Get-DataScript 'post')
            Write-Output @"

已產生 $output：
- DeployReport.xml：差異清單；Upgrade.sql：升級 SQL（SSMS 以 SQLCMD 模式執行）。
審核後依序執行：
1. -Action Pre$(if ($pre.Count) { "（$($pre.Count) 個 pre 腳本）" } else { '（這一版沒有 pre 腳本，可略過）' })
2. DBA 執行 Upgrade.sql
3. -Action Post（FullText、$($post.Count) 個 post 腳本、Seed）
4. Verify-Database.ps1 比對實際結構
"@
        } else {
            if ($exists) { Invoke-Pre }
            $report = Join-Path ($output ?? $work) 'DeployReport.xml'
            $arguments = @('/Action:Publish', "/SourceFile:$($layout.Dacpac)", "@$response", "/DeployReportPath:$report")
            if ($output) { $arguments += "/DeployScriptPath:$(Join-Path $output 'Upgrade.sql')" }
            Invoke-SqlPackage $sqlPackage ($arguments + (Get-DeployProperty (!$exists)))
            Write-ReportSummary $report
            Invoke-Post
            Write-Output "$Database 已部署到 $Server。"
        }
    } finally {
        Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
    }
} elseif ($Action -eq 'Pre') { Invoke-Pre }
else { Invoke-Post; Write-Output "Post 完成。接著以 Verify-Database.ps1 比對實際結構。" }
