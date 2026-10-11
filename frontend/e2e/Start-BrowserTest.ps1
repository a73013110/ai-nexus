#requires -Version 7.4

<#
.SYNOPSIS
以隔離的空設定啟動發布產物，給 Playwright e2e 使用（由 frontend/e2e/playwright.config.ts 呼叫）。

.DESCRIPTION
不載入這台機器的 .local 或正式設定；資料庫刻意指向不存在的位址，API 資料由測試替身提供。
設定、金鑰、附件與日誌放在 artifacts/browser-server-*。

.PARAMETER Port
網站埠號。

.PARAMETER PublishDirectory
發布產物目錄，必須在 artifacts/ 內；預設讀環境變數 NEXUS_E2E_PUBLISH_DIRECTORY，再預設 artifacts/publish。

.EXAMPLE
./frontend/e2e/Start-BrowserTest.ps1 -Port 5180
#>
[CmdletBinding()]
param(
    [ValidateRange(1024, 65535)][int]$Port = 5180,
    [string]$PublishDirectory = $(if ($env:NEXUS_E2E_PUBLISH_DIRECTORY) { $env:NEXUS_E2E_PUBLISH_DIRECTORY } else { 'artifacts/publish' })
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot '../../scripts/AiNexus') -Force

$publish = Resolve-NexusArtifactPath $PublishDirectory
$artifacts = Join-Path (Get-NexusRoot) 'artifacts'
$dll = Join-Path $publish 'AiNexus.Host.dll'
if (!(Test-Path -LiteralPath $dll)) { throw '找不到發布產物；請先執行 scripts/Build.ps1。' }
$config = Join-Path $artifacts 'browser-server-config'
New-Item -ItemType Directory -Path $config -Force | Out-Null
$settings = Join-Path $config 'appsettings.Local.json'
$secrets = Join-Path $config 'appsettings.Secrets.json'
[IO.File]::WriteAllText($settings, '{}')
[IO.File]::WriteAllText($secrets, '{}')
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ConnectionStrings__Nexus = ''
$env:Database__User = ''
$env:Database__Password = ''
$env:Database__Server = '127.0.0.1,1'
$env:Database__ApplyMigrationsOnStartup = 'false'
$env:Identity__ActiveDirectory__Mode = 'Windows'
$env:DataProtection__KeyRingPath = Join-Path $artifacts 'browser-server-keys'
$env:Attachments__StoragePath = Join-Path $artifacts 'browser-server-attachments'
$env:Diagnostics__Directory = Join-Path $artifacts 'browser-server-diagnostics'
$env:Diagnostics__OtlpEnabled = 'false'
$env:Diagnostics__SqlTimeoutSeconds = '1'
dotnet $dll --contentRoot $publish --urls "http://localhost:$Port" --Security:AllowInsecureLocalhost true --LocalConfigPath $settings --SecretsConfigPath $secrets
exit $LASTEXITCODE
