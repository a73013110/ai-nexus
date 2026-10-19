#requires -Version 7.4

<#
.SYNOPSIS
還原所有鎖定版本的相依套件。

.DESCRIPTION
依 lockfile 還原 npm（frontend，含 Playwright）、NuGet、資料庫專案（database/App）與 dotnet 本機工具（SqlPackage），
並在缺少時為目前使用者安裝 Verify.ps1 需要的 Pester。

.COMPONENT
初次設定

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
Push-Location -LiteralPath (Get-RepoRoot)
try {
    npm --prefix frontend ci --no-fund --no-audit
    if ($LASTEXITCODE -ne 0) { throw '前端 npm 相依套件還原失敗。' }
    dotnet restore backend/AiNexus.slnx --locked-mode
    if ($LASTEXITCODE -ne 0) { throw '後端相依套件還原失敗。' }
    # 資料庫專案只有 Microsoft.Build.Sql SDK（版本寫在 sqlproj），沒有套件參照，所以不需要 lockfile。
    dotnet restore database/App
    if ($LASTEXITCODE -ne 0) { throw '資料庫專案還原失敗。' }
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw '.NET 本機工具還原失敗。' }
    Install-RepoPester
    Write-Output '已還原鎖定版本的相依套件、EF 工具、SqlPackage 與 Pester。'
} finally { Pop-Location }
