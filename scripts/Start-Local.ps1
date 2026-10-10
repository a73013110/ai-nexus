#requires -Version 7.4

<#
.SYNOPSIS
以單一 ASP.NET 程序執行整合預覽（Angular 產物＋API）。

.DESCRIPTION
預設先執行 Build.ps1，再以 Development 環境與 .local/ 的設定啟動發布產物。Ctrl+C 停止。

.PARAMETER SkipBuild
直接執行既有的發布產物。

.PARAMETER Http
改用 HTTP；只在 localhost 有效，用於自動化測試。

.PARAMETER Port
網站埠號。

.PARAMETER PublishDirectory
發布目錄，必須在 artifacts/ 內；未指定時讀環境變數 NEXUS_E2E_PUBLISH_DIRECTORY，再預設 artifacts/publish。

.EXAMPLE
./scripts/Start-Local.ps1

.EXAMPLE
./scripts/Start-Local.ps1 -SkipBuild -PublishDirectory artifacts/verification -Port 5081
#>
[CmdletBinding()]
param(
    [switch]$SkipBuild,
    [switch]$Http,
    [ValidateRange(1024, 65535)][int]$Port = 5080,
    [string]$PublishDirectory = $(if ($env:NEXUS_E2E_PUBLISH_DIRECTORY) { $env:NEXUS_E2E_PUBLISH_DIRECTORY } else { 'artifacts/publish' })
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'AiNexus') -Force

if (!$Http) { Assert-NexusHttpsCertificate }
$local = Initialize-NexusLocalSettings
$publish = Resolve-NexusArtifactPath $PublishDirectory
if (!$SkipBuild) { & (Join-Path $PSScriptRoot 'Build.ps1') -OutputDirectory $publish }
$dll = Join-Path $publish 'AiNexus.Host.dll'
if (!(Test-Path -LiteralPath $dll)) { throw '找不到發布產物；請先執行 scripts/Build.ps1。' }
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$scheme = if ($Http) { 'http' } else { 'https' }
dotnet $dll --contentRoot $publish --urls "${scheme}://localhost:$Port" --Security:AllowInsecureLocalhost $Http.IsPresent --LocalConfigPath $local.Settings --SecretsConfigPath $local.Secrets
exit $LASTEXITCODE
