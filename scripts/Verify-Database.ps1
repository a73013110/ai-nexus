#requires -Version 7.4

<#
.SYNOPSIS
部署後比對正式庫的實際結構與發布套件的 App.dacpac；有任何結構差異就列出物件並以結束碼 1 失敗。

.DESCRIPTION
DBA 依 dacpac 產生的升級 SQL 可能漏跑或被改過，正式機也可能有未提交的熱修。這個腳本以 SqlPackage
DeployReport 比對實際結構（唯讀，不修改資料庫）：忽略欄位順序，排除使用者、登入、角色、權限、檔案群組、資料庫選項等
環境差異；正式庫缺少或多出的物件都算差異。全文目錄與全文索引不比對，因為參考庫與正式機是否安裝全文元件可能不同；
全文索引改用主機指令 verify sql 檢查。隨發布套件複製到主機，不依賴 repo；需要 SqlPackage，找不到時會說明安裝方式。

.PARAMETER Server
正式 SQL Server，例如 sql.company.internal 或 sql.company.internal,1433。

.PARAMETER Database
要驗證的資料庫，預設 AiNexus。

.PARAMETER DacpacPath
發布套件的 database\App.dacpac（Publish-IIS.ps1 產生）。

.PARAMETER Credential
SQL 登入（需要 VIEW DEFINITION，例如部署帳號）；省略時以目前 Windows 身分連線。

.PARAMETER TrustServerCertificate
SQL Server 使用自簽憑證時略過憑證鏈驗證；連線仍加密。

.PARAMETER SqlPackagePath
SqlPackage 執行檔或 .dll；省略時依序找 PATH 上的 sqlpackage 與 DacFx 的預設安裝位置。

.PARAMETER ReportPath
保留 DeployReport XML 的位置；省略時只輸出摘要。

.EXAMPLE
./Verify-Database.ps1 -Server sql.company.internal -DacpacPath .\database\App.dacpac

.EXAMPLE
./Verify-Database.ps1 -Server 192.0.2.10 -DacpacPath .\database\App.dacpac -Credential (Get-Credential) -TrustServerCertificate -ReportPath .\logs\deploy-report.xml
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Server,
    [ValidateNotNullOrEmpty()][string]$Database = 'AiNexus',
    [Parameter(Mandatory)][string]$DacpacPath,
    [pscredential]$Credential,
    [switch]$TrustServerCertificate,
    [string]$SqlPackagePath,
    [string]$ReportPath
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# The version the repository pins in .config/dotnet-tools.json (scripts/tests keeps them equal).
$sqlPackageVersion = '170.5.96'

# Environment, not schema: who may sign in and what they may do, where files live, and database-level options (RCSI is
# checked by the site at startup). Full-text catalogs are excluded here; full-text indexes have no exclusion and are
# filtered from the report below.
$excluded = @(
    'Users', 'Logins', 'DatabaseRoles', 'ApplicationRoles', 'RoleMembership', 'Permissions', 'Credentials', 'DatabaseScopedCredentials',
    'ServerRoles', 'ServerRoleMembership', 'ServerTriggers', 'LinkedServers', 'LinkedServerLogins', 'Endpoints', 'ErrorMessages',
    'Audits', 'ServerAuditSpecifications', 'DatabaseAuditSpecifications', 'EventSessions', 'Certificates', 'MasterKeys',
    'DatabaseEncryptionKeys', 'CryptographicProviders', 'Filegroups', 'Files', 'DatabaseOptions', 'FullTextCatalogs', 'FullTextStoplists')
$properties = @(
    'IgnoreColumnOrder=True', 'DropObjectsNotInSource=True', "ExcludeObjectTypes=$($excluded -join ';')",
    'IgnorePermissions=True', 'IgnoreRoleMembership=True', 'IgnoreLoginSids=True', 'IgnoreAuthorizer=True', 'IgnoreUserSettingsObjects=True',
    'IgnoreFilegroupPlacement=True', 'IgnoreFileAndLogFilePath=True', 'IgnoreFileSize=True', 'IgnoreFullTextCatalogFilePath=True',
    'ScriptDatabaseOptions=False', 'ScriptDatabaseCollation=False', 'ScriptDatabaseCompatibility=False',
    # Only a report is produced; blocking on data loss would stop it before listing what differs.
    'BlockOnPossibleDataLoss=False')
$fullTextTypes = @('SqlFullTextIndex', 'SqlFullTextCatalog', 'SqlFullTextStopList')

function Find-SqlPackage {
    if ($SqlPackagePath) {
        if (!(Test-Path -LiteralPath $SqlPackagePath -PathType Leaf)) { throw "找不到 -SqlPackagePath：$SqlPackagePath" }
        return (Resolve-Path -LiteralPath $SqlPackagePath).Path
    }
    $command = Get-Command sqlpackage -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($command) { return $command.Source }
    if ($env:ProgramFiles) {
        foreach ($version in '170', '160') {
            $candidate = Join-Path $env:ProgramFiles "Microsoft SQL Server/$version/DAC/bin/SqlPackage.exe"
            if (Test-Path -LiteralPath $candidate -PathType Leaf) { return $candidate }
        }
    }
    throw @"
找不到 SqlPackage，無法做部署後結構驗證（不會略過）。請擇一安裝後重試，或以 -SqlPackagePath 指定 SqlPackage.exe：
- 有 .NET SDK 的機器：dotnet tool install --global microsoft.sqlpackage --version $sqlPackageVersion（重開終端機後 sqlpackage 在 PATH 上）
- 只有 Hosting Bundle 的 IIS 主機：從 https://learn.microsoft.com/sql/tools/sqlpackage/sqlpackage-download 下載 Windows 版 zip，解壓後以 -SqlPackagePath 指定其中的 SqlPackage.exe；或安裝 DacFx（SqlPackage.exe 在 Program Files\Microsoft SQL Server\170\DAC\bin）
- 也可以在能連到正式 SQL 的 DBA 工作站執行本腳本
"@
}

# SqlPackage 在 response file 中把 \" 當成引號，無法解析的引數會原樣印出；
# 所以連線字串要小心加引號，且結尾不可是反斜線。
function Format-ResponseArgument([string]$Name, [string]$Value) {
    if ($Value -match '[\r\n]') { throw "$Name 不能含換行。" }
    if ($Value.EndsWith('\')) { $Value += ';' }
    "/$($Name):`"$($Value.Replace('"', '\"'))`""
}

$dacpac = (Resolve-Path -LiteralPath $DacpacPath -ErrorAction SilentlyContinue)?.Path
if (!$dacpac -or !(Test-Path -LiteralPath $dacpac -PathType Leaf)) { throw "找不到 App.dacpac：$DacpacPath。它在 Publish-IIS.ps1 產生的套件 database\ 內。" }
$sqlPackage = Find-SqlPackage

$connection = [Data.Common.DbConnectionStringBuilder]::new()
$connection['Server'] = $Server
$connection['Database'] = $Database
$connection['Encrypt'] = 'True'
$connection['TrustServerCertificate'] = [string][bool]$TrustServerCertificate
$connection['Persist Security Info'] = 'False'
if ($Credential) {
    $connection['User ID'] = $Credential.UserName
    $connection['Password'] = $Credential.GetNetworkCredential().Password
} else { $connection['Integrated Security'] = 'True' }

# The password only ever lands in this per-user temporary folder, which is deleted before the script ends.
$work = Join-Path ([IO.Path]::GetTempPath()) ('AiNexus-verify-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null
try {
    $response = Join-Path $work 'connection.rsp'
    [IO.File]::WriteAllText($response, (Format-ResponseArgument 'TargetConnectionString' $connection.ConnectionString))
    $connection = $null
    $report = if ($ReportPath) { [IO.Path]::GetFullPath($ReportPath) } else { Join-Path $work 'deploy-report.xml' }
    $arguments = @('/Action:DeployReport', "/SourceFile:$dacpac", "@$response", "/OutputPath:$report") + @($properties | ForEach-Object { "/p:$_" })
    if ($sqlPackage -like '*.dll') { & dotnet $sqlPackage @arguments | Out-Host } else { & $sqlPackage @arguments | Out-Host }
    if ($LASTEXITCODE -ne 0) { throw "SqlPackage DeployReport 失敗（結束碼 $LASTEXITCODE）；原因見上方輸出（連線、權限需要 VIEW DEFINITION）。" }
    [IO.File]::Delete($response)

    [xml]$xml = [IO.File]::ReadAllText($report)
    $namespace = [Xml.XmlNamespaceManager]::new($xml.NameTable)
    $namespace.AddNamespace('r', $xml.DocumentElement.NamespaceURI)
    $items = foreach ($operation in $xml.SelectNodes('//r:Operations/r:Operation', $namespace)) {
        foreach ($item in $operation.SelectNodes('r:Item', $namespace)) {
            [pscustomobject]@{ Operation = $operation.GetAttribute('Name'); Type = $item.GetAttribute('Type'); Object = $item.GetAttribute('Value') }
        }
    }
    $differences = @($items | Where-Object { $_.Type -notin $fullTextTypes })
    $fullText = @($items | Where-Object { $_.Type -in $fullTextTypes })
    $alerts = @(foreach ($alert in $xml.SelectNodes('//r:Alerts/r:Alert', $namespace)) {
        foreach ($issue in $alert.SelectNodes('r:Issue', $namespace)) { [pscustomobject]@{ Alert = $alert.GetAttribute('Name'); Detail = $issue.GetAttribute('Value') } }
    })
    if ($fullText.Count) {
        Write-Output "略過 $($fullText.Count) 項全文目錄／索引差異（不比對，見說明）；全文索引請用主機指令 verify sql 確認："
        $fullText | Format-Table -AutoSize | Out-String -Width 200 | ForEach-Object TrimEnd | Write-Output
    }
    if ($differences.Count) {
        Write-Output "$Database 的結構與 App.dacpac 不同（$($differences.Count) 項）。Operation 是要讓正式庫符合預期所需的動作；通常是漏執行的升級 SQL 或未提交的熱修："
        $differences | Format-Table -AutoSize | Out-String -Width 200 | ForEach-Object TrimEnd | Write-Output
        if ($alerts.Count) { $alerts | Format-Table -AutoSize -Wrap | Out-String -Width 200 | ForEach-Object TrimEnd | Write-Output }
        exit 1
    }
    Write-Output "$Database 的結構符合 App.dacpac（忽略欄位順序與環境差異；全文物件不比對）。"
} finally {
    Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
}
