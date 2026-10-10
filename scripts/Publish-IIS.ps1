#requires -Version 7.4

<#
.SYNOPSIS
產生新的 IIS 發布套件（app、config 範本、keys、logs 與 Verify-IIS.ps1）。

.DESCRIPTION
預設先執行 Build.ps1，再把發布產物複製到 artifacts/iis/<時間>/app。config/ 只在不存在時由範本建立，
並把 Attachments.StoragePath 與 Diagnostics.Directory 設為 -DataRoot 下的 attachments、diagnostics。
套件不含秘密；只有 app/ 是 IIS 的實體路徑。替換執行中網站的步驟見 deploy/iis/README.md。

.PARAMETER DataRoot
正式主機上站外資料的根目錄（絕對路徑），例如 D:\AiNexus\data。

.PARAMETER DestinationPath
新套件的 app 目錄，必須是空的；預設 artifacts/iis/<UTC 時間>/app。不要指向執行中的 IIS 目錄。

.PARAMETER Environment
寫入 web.config 的 ASPNETCORE_ENVIRONMENT。

.PARAMETER IncludeLocalConfig
以這台機器 .local/ 的設定與秘密取代範本（只用於自己的測試主機）。

.PARAMETER AllowUntrustedSql
設定 Database.TrustServerCertificate=true，用於自簽憑證的 SQL Server。

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
    [ValidateSet('Production', 'Staging')][string]$Environment = 'Production',
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
$root = Get-NexusRoot
$publish = Resolve-NexusArtifactPath $PublishDirectory
if (!$SkipBuild) { & (Join-Path $PSScriptRoot 'Build.ps1') -OutputDirectory $publish }
if (!(Test-Path -LiteralPath (Join-Path $publish 'AiNexus.Host.dll'))) { throw '找不到發布產物；請先執行 scripts/Build.ps1。' }
if (!$DestinationPath) { $DestinationPath = Join-Path $root ('artifacts/iis/' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfff') + '/app') }
$app = [IO.Path]::GetFullPath($DestinationPath)
if ((Test-Path -LiteralPath $app) -and @(Get-ChildItem -LiteralPath $app -Force).Count) {
    throw 'DestinationPath 必須是新套件的空 app 目錄；替換執行中的網站請依 deploy/iis/README.md。'
}
$package = Split-Path $app
foreach ($directory in @($app, (Join-Path $package 'config'), (Join-Path $package 'keys'), (Join-Path $package 'logs'))) {
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
}
Get-ChildItem -LiteralPath $publish | Where-Object { $_.Name -notin @('logs', 'App_Data') } | Copy-Item -Destination $app -Recurse

$settingsPath = Join-Path $package "config/appsettings.$Environment.json"
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
if ($AllowUntrustedSql) { Set-NexusSetting $settings 'Database.TrustServerCertificate' $true }
Save-NexusJson $settingsPath $settings
$settings = $null

[xml]$web = [IO.File]::ReadAllText((Join-Path $app 'web.config'))
$web.SelectSingleNode("//environmentVariable[@name='ASPNETCORE_ENVIRONMENT']").SetAttribute('value', $Environment)
$web.SelectSingleNode("//environmentVariable[@name='LocalConfigPath']").SetAttribute('value', "..\config\appsettings.$Environment.json")
$web.Save((Join-Path $app 'web.config'))
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Verify-IIS.ps1') -Destination $package
foreach ($directory in @('db', 'docs', 'deploy/iis', 'contracts')) {
    $target = Join-Path $package $directory
    New-Item -ItemType Directory -Path $target -Force | Out-Null
    Get-ChildItem -LiteralPath (Join-Path $root $directory) -File | Copy-Item -Destination $target
}
Write-Output "IIS 發布套件：$package"
Write-Output "只有 app/ 是 IIS 實體路徑。更新時保留執行中的 config/、keys/ 與 $DataRoot；首次啟動前建立站外資料目錄並給 application pool Modify 權限，見 deploy/iis/README.md。套件的 logs/ 只給 ANCM stdout 使用。"
