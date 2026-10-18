#requires -Version 7.4

<#
.SYNOPSIS
產生新的 IIS 發布套件（app、config 範本、keys、logs、database/、Verify-IIS.ps1、Deploy-Database.ps1 與 Verify-Database.ps1）。

.DESCRIPTION
預設先執行 Build.ps1，再把發布產物複製到 artifacts/iis/<時間>/app；web.config 由 SDK 從
backend/src/AiNexus.Host/web.config 帶入，不再改寫。config/ 只在不存在時由範本建立，
並把 Attachments.StoragePath 與 Diagnostics.Directory 設為 -DataRoot 下的 attachments、diagnostics。
database/ 帶 App.dacpac（由 database/App 建置，這一版預期的資料庫結構）與發版時執行的手寫腳本 FullText/、Seed/、Data/；
DBA 以 Deploy-Database.ps1 產生升級 SQL 並依序執行，部署後以 Verify-Database.ps1 比對實際結構。
套件不含秘密；只有 app/ 是 IIS 的實體路徑。替換執行中網站的步驟見 docs/operations/iis-deployment.md。

.PARAMETER DataRoot
正式主機上站外資料的根目錄（絕對路徑），例如 D:\AiNexus\data。

.PARAMETER DestinationPath
新套件的 app 目錄，必須是空的；預設 artifacts/iis/<UTC 時間>/app。不要指向執行中的 IIS 目錄。

.PARAMETER IncludeLocalConfig
以這台機器 .local/ 的設定與秘密取代範本（只用於自己的測試主機）。

.PARAMETER AllowUntrustedSql
設定 Databases.App.TrustServerCertificate=true，用於自簽憑證的 SQL Server。

.PARAMETER SkipBuild
使用 PublishDirectory 既有的發布產物。

.PARAMETER PublishDirectory
發布產物目錄，必須在 artifacts/ 內。

.EXAMPLE
./scripts/Publish-IIS.ps1 -DataRoot 'D:\AiNexus\data'

.EXAMPLE
./scripts/Publish-IIS.ps1 -DataRoot 'D:\AiNexus\data' -SkipBuild -PublishDirectory artifacts/verification
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$DataRoot,
    [string]$DestinationPath,
    [switch]$IncludeLocalConfig,
    [switch]$AllowUntrustedSql,
    [switch]$SkipBuild,
    [string]$PublishDirectory = 'artifacts/publish'
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'AiNexus') -Force

# Windows paths are checked on any OS, because the package is built for a Windows IIS host.
if ($DataRoot -notmatch '^[A-Za-z]:\\') { throw 'DataRoot 必須是正式主機上的絕對路徑，例如 D:\AiNexus\data。' }
$root = Get-RepoRoot
$publish = Resolve-RepoArtifactPath $PublishDirectory
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
$sources = if ($IncludeLocalConfig) { Initialize-RepoLocalSettings } else {
    @{ Settings = Join-Path $templates 'appsettings.Production.example.json'; Secrets = Join-Path $templates 'appsettings.Secrets.example.json' }
}
if (!(Test-Path -LiteralPath $settingsPath)) { Copy-Item -LiteralPath $sources.Settings -Destination $settingsPath }
if (!(Test-Path -LiteralPath $secretsPath)) { Copy-Item -LiteralPath $sources.Secrets -Destination $secretsPath; Protect-RepoSecrets $secretsPath }
$settings = Read-RepoJson $settingsPath
Set-RepoSetting $settings 'Attachments.StoragePath' ($DataRoot.TrimEnd('\') + '\attachments')
Set-RepoSetting $settings 'Diagnostics.Directory' ($DataRoot.TrimEnd('\') + '\diagnostics')
if ($AllowUntrustedSql) { Set-RepoSetting $settings 'Databases.App.TrustServerCertificate' $true }
Save-RepoJson $settingsPath $settings
$settings = $null

Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Verify-IIS.ps1') -Destination $package

# 與 app/ 同一份原始碼建置，DBA 拿到的預期結構不會和程式脫節。
Push-Location -LiteralPath $root
try {
    dotnet build database/App --no-restore -c Release
    if ($LASTEXITCODE -ne 0) { throw '資料庫專案 build 失敗；請先執行 scripts/Restore.ps1。' }
} finally { Pop-Location }
$database = Join-Path $package 'database'
New-Item -ItemType Directory -Path $database -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'database/App/bin/Release/App.dacpac') -Destination $database
# 手寫腳本不進 dacpac，由 Deploy-Database.ps1 在結構之前（Data/<版本>/pre-*）或之後執行；這一版沒有的資料夾就不帶。
foreach ($folder in 'FullText', 'Seed', 'Data') {
    $source = Join-Path $root "database/App/$folder"
    if (Test-Path -LiteralPath $source) { Copy-Item -LiteralPath $source -Destination $database -Recurse }
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Deploy-Database.ps1'), (Join-Path $PSScriptRoot 'Verify-Database.ps1') -Destination $package
Write-Output "IIS 發布套件：$package"
Write-Output "只有 app/ 是 IIS 實體路徑。更新時保留執行中的 config/、keys/ 與 $DataRoot；首次啟動前建立站外資料目錄並給 application pool Modify 權限，見 docs/operations/iis-deployment.md。套件的 logs/ 只給 ANCM stdout 使用。"
