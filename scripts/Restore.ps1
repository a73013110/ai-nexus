#requires -Version 7.4

<#
.SYNOPSIS
還原所有鎖定版本的相依套件。

.DESCRIPTION
依 lockfile 還原 npm（根目錄 e2e、frontend）、NuGet 與 dotnet 本機工具（dotnet-ef），
並在缺少時為目前使用者安裝 Verify.ps1 需要的 Pester。

.EXAMPLE
./scripts/Restore.ps1
#>
[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'AiNexus') -Force

$env:NG_CLI_ANALYTICS = 'false'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
Push-Location -LiteralPath (Get-NexusRoot)
try {
    foreach ($folder in @('.', 'frontend')) {
        npm --prefix $folder ci --no-fund --no-audit
        if ($LASTEXITCODE -ne 0) { throw "npm 相依套件還原失敗：$folder" }
    }
    dotnet restore backend/AiNexus.slnx --locked-mode
    if ($LASTEXITCODE -ne 0) { throw '後端相依套件還原失敗。' }
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw '.NET 本機工具還原失敗。' }
    Install-NexusPester
    Write-Output '已還原鎖定版本的相依套件、EF 工具與 Pester。'
} finally { Pop-Location }
