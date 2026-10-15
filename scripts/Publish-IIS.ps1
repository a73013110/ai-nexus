#requires -Version 7.4

<#
.SYNOPSIS
產生新的 IIS 發布套件（app、config 範本、keys、logs、migrations.sql、選用的 database/、Verify-IIS.ps1 與 Verify-Database.ps1）。

.DESCRIPTION
預設先執行 Build.ps1，再把發布產物複製到 artifacts/iis/<時間>/app；web.config 由 SDK 從
backend/src/AiNexus.Host/web.config 帶入，不再改寫。config/ 只在不存在時由範本建立，
並把 Attachments.StoragePath 與 Diagnostics.Directory 設為 -DataRoot 下的 attachments、diagnostics。
migrations.sql 是發布當下以 dotnet ef 產生的 idempotent SQL，給 DBA 審閱套用。
提供 -FromMigration 時另以主機的 db script 指令產生 database/：同一份 SQL 依物件與結構／資料拆成編號檔，附 manifest.md；
再以 db diff 指令（SqlPackage）產生 database/diff/ 與 database/expected.dacpac，部署後以 Verify-Database.ps1 驗證實際結構。
套件不含秘密；只有 app/ 是 IIS 的實體路徑。替換執行中網站的步驟見 docs/operations/iis-deployment.md。

.PARAMETER DataRoot
正式主機上站外資料的根目錄（絕對路徑），例如 D:\AiNexus\data。

.PARAMETER DestinationPath
新套件的 app 目錄，必須是空的；預設 artifacts/iis/<UTC 時間>/app。不要指向執行中的 IIS 目錄。

.PARAMETER IncludeLocalConfig
以這台機器 .local/ 的設定與秘密取代範本（只用於自己的測試主機）。

.PARAMETER AllowUntrustedSql
設定 Databases.Nexus.TrustServerCertificate=true，用於自簽憑證的 SQL Server。

.PARAMETER FromMigration
正式機目前最後一個已套用的 migration（id 或名稱；0 代表空庫）。提供時在套件產生 database/（schema/、data/ 與
manifest.md），範圍到最新 migration；另在 Databases.Nexus 的 SQL Server 建暫存庫，產生每個物件的差異（diff/）與部署後
驗證用的 expected.dacpac，並附 Verify-Database.ps1。暫存庫結束時一定刪除。

.PARAMETER SkipSchemaDiff
搭配 -FromMigration：只產生拆檔 SQL，不連 SQL Server；套件沒有 diff/、expected.dacpac 與 Verify-Database.ps1。

.PARAMETER SkipBuild
使用 PublishDirectory 既有的發布產物。

.PARAMETER PublishDirectory
發布產物目錄，必須在 artifacts/ 內。

.EXAMPLE
./scripts/Publish-IIS.ps1 -DataRoot 'D:\AiNexus\data'

.EXAMPLE
./scripts/Publish-IIS.ps1 -DataRoot 'D:\AiNexus\data' -SkipBuild -PublishDirectory artifacts/verification

.EXAMPLE
./scripts/Publish-IIS.ps1 -DataRoot 'D:\AiNexus\data' -FromMigration 20261014140318_NavigationGroups
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$DataRoot,
    [string]$DestinationPath,
    [switch]$IncludeLocalConfig,
    [switch]$AllowUntrustedSql,
    [ValidateNotNullOrEmpty()][string]$FromMigration,
    [switch]$SkipSchemaDiff,
    [switch]$SkipBuild,
    [string]$PublishDirectory = 'artifacts/publish'
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'AiNexus') -Force

# Windows paths are checked on any OS, because the package is built for a Windows IIS host.
if ($DataRoot -notmatch '^[A-Za-z]:\\') { throw 'DataRoot 必須是正式主機上的絕對路徑，例如 D:\AiNexus\data。' }
$root = Get-NexusRoot
$publish = Resolve-NexusArtifactPath $PublishDirectory
if (!$SkipBuild) { & (Join-Path $PSScriptRoot 'Build.ps1') -OutputDirectory $publish }
if (!(Test-Path -LiteralPath (Join-Path $publish 'AiNexus.Host.dll'))) { throw '找不到發布產物；請先執行 scripts/Build.ps1。' }
if (!$DestinationPath) { $DestinationPath = Join-Path $root ('artifacts/iis/' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfff') + '/app') }
$app = [IO.Path]::GetFullPath($DestinationPath)
if ((Test-Path -LiteralPath $app) -and @(Get-ChildItem -LiteralPath $app -Force).Count) {
    throw 'DestinationPath 必須是新套件的空 app 目錄；替換執行中的網站請依 docs/operations/iis-deployment.md。'
}
$package = Split-Path $app
foreach ($directory in @($app, (Join-Path $package 'config'), (Join-Path $package 'keys'), (Join-Path $package 'logs'))) {
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
}
Get-ChildItem -LiteralPath $publish | Where-Object { $_.Name -notin @('logs', 'App_Data') } | Copy-Item -Destination $app -Recurse

$settingsPath = Join-Path $package 'config/appsettings.Production.json'
$secretsPath = Join-Path $package 'config/appsettings.Secrets.json'
$templates = Join-Path $root 'backend/src/AiNexus.Host'
$sources = if ($IncludeLocalConfig) { Initialize-NexusLocalSettings } else {
    @{ Settings = Join-Path $templates 'appsettings.Production.example.json'; Secrets = Join-Path $templates 'appsettings.Secrets.example.json' }
}
if (!(Test-Path -LiteralPath $settingsPath)) { Copy-Item -LiteralPath $sources.Settings -Destination $settingsPath }
if (!(Test-Path -LiteralPath $secretsPath)) { Copy-Item -LiteralPath $sources.Secrets -Destination $secretsPath; Protect-NexusSecrets $secretsPath }
$settings = Read-NexusJson $settingsPath
Set-NexusSetting $settings 'Attachments.StoragePath' ($DataRoot.TrimEnd('\') + '\attachments')
Set-NexusSetting $settings 'Diagnostics.Directory' ($DataRoot.TrimEnd('\') + '\diagnostics')
if ($AllowUntrustedSql) { Set-NexusSetting $settings 'Databases.Nexus.TrustServerCertificate' $true }
Save-NexusJson $settingsPath $settings
$settings = $null

Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Verify-IIS.ps1') -Destination $package

# Generated from the same source as app/, so the SQL a DBA reviews never drifts from the shipped migrations.
Push-Location -LiteralPath $root
try {
    dotnet ef migrations script --idempotent --configuration Release --project backend/src/AiNexus.Features --startup-project backend/src/AiNexus.Host --output (Join-Path $package 'migrations.sql')
    if ($LASTEXITCODE -ne 0) { throw 'migration SQL 產生失敗；請先執行 scripts/Restore.ps1 還原 dotnet-ef。' }
} finally { Pop-Location }
# Same migrations, split for DBAs who run DDL and DML separately and file by file; see docs/operations/iis-deployment.md.
if ($PSBoundParameters.ContainsKey('FromMigration')) {
    $database = Join-Path $package 'database'
    if ((Invoke-NexusHostCommand 'db', 'script', '--from', $FromMigration, '--output', $database) -ne 0) {
        throw '拆檔 SQL 產生失敗；原因見上方輸出（例如 -FromMigration 不存在，或 migration 含會重建資料表的操作）。'
    }
    # Per-object diff and the expected schema for Verify-Database.ps1, from a temporary database on the Databases:Nexus
    # server. The host runs SqlPackage with dotnet tool run, so the version pinned in .config/dotnet-tools.json.
    if (!$SkipSchemaDiff) {
        Push-Location -LiteralPath $root
        try {
            dotnet tool restore | Out-Host
            if ($LASTEXITCODE -ne 0) { throw '.NET 本機工具（SqlPackage）還原失敗。' }
            if ((Invoke-NexusHostCommand 'db', 'diff', '--from', $FromMigration, '--output', $database) -ne 0) {
                throw '結構差異產生失敗；原因見上方輸出（Databases.Nexus 需可連線且能建庫）。不需要時加 -SkipSchemaDiff。'
            }
        } finally { Pop-Location }
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Verify-Database.ps1') -Destination $package
    }
}
Write-Output "IIS 發布套件：$package"
Write-Output "只有 app/ 是 IIS 實體路徑。更新時保留執行中的 config/、keys/ 與 $DataRoot；首次啟動前建立站外資料目錄並給 application pool Modify 權限，見 docs/operations/iis-deployment.md。套件的 logs/ 只給 ANCM stdout 使用。"
