#requires -Version 7.4

<#
.SYNOPSIS
以 .local/ 的連線設定建立一個新的 AiNexus 資料庫：結構、全文物件與種子資料。

.DESCRIPTION
先 build database/App，再以 Deploy-Database.ps1 -Action Publish 部署。只建立新庫：資料庫已存在時停止，
不會改動 DBA 維護的開發庫；既有庫的升級走 Deploy-Database.ps1 -Action Script（見 docs/operations/database-setup.md）。
需要有建庫權限的 SQL 登入。

.PARAMETER Server
建在另一台 SQL Server（例如 (localdb)\MSSQLLocalDB），以目前 Windows 身分連線；省略時用設定的 Databases.App 與秘密檔的帳密。

.PARAMETER Database
要建立的資料庫，省略時用設定的 Databases.App.Name；例如另建一個沙盒庫。

.EXAMPLE
./scripts/Initialize-Database.ps1

.EXAMPLE
./scripts/Initialize-Database.ps1 -Server '(localdb)\MSSQLLocalDB' -Database AiNexus_Sandbox
#>
[CmdletBinding()]
param([string]$Server, [string]$Database)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'AiNexus') -Force

$root = Get-RepoRoot
$paths = Initialize-RepoLocalSettings
$defaults = Read-RepoJson (Join-Path $root 'backend/src/AiNexus.Host/appsettings.json')
$local = Read-RepoJson $paths.Settings
$secrets = Read-RepoJson $paths.Secrets
function Get-DatabaseSetting([string]$Key) {
    foreach ($settings in $secrets, $local, $defaults) {
        $value = Get-RepoSetting $settings "Databases.App.$Key"
        if ($null -ne $value -and "$value" -ne '') { return $value }
    }
}
if (!$Database) { $Database = Get-DatabaseSetting 'Name' }
$connection = if ($Server) { @{ Server = $Server; Database = $Database } } else {
    @{ Server = (Get-DatabaseSetting 'Server'); Database = $Database; TrustServerCertificate = [bool](Get-DatabaseSetting 'TrustServerCertificate') }
}
if (!$connection.Server -or !$Database) { throw '請先以 scripts/Configure-Local.ps1 設定 SQL server 與資料庫名稱。' }
$user = Get-DatabaseSetting 'User'
if (!$Server -and !(Get-DatabaseSetting 'IntegratedSecurity') -and $user) {
    $connection.Credential = [pscredential]::new($user, (ConvertTo-SecureString (Get-DatabaseSetting 'Password') -AsPlainText -Force))
}
$secrets = $null

if (Test-RepoDatabaseExists @connection) {
    throw "$($connection.Server) 上已有 $Database，這個腳本只建立新庫。升級既有庫用 scripts/Deploy-Database.ps1（見 docs/operations/database-setup.md）。"
}
Push-Location -LiteralPath $root
try {
    dotnet build database/App -c Release --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw '資料庫專案 build 失敗；請先執行 scripts/Restore.ps1。' }
} finally { Pop-Location }
& (Join-Path $PSScriptRoot 'Deploy-Database.ps1') @connection -Action Publish
