#requires -Version 7.4

<#
.SYNOPSIS
建置 Angular 並發布 .NET，產出可直接執行的網站。

.DESCRIPTION
先 build 前端，再 dotnet publish 主機，最後把前端產物放進發布目錄的 wwwroot。
輸出目錄必須在 artifacts/ 內；舊的 wwwroot 只有在確認是此 repo 內的實體目錄時才會刪除。

.PARAMETER Restore
先執行 Restore.ps1。

.PARAMETER OutputDirectory
發布目錄，相對於 repo 根目錄，必須在 artifacts/ 內。

.EXAMPLE
./scripts/Build.ps1 -Restore

.EXAMPLE
./scripts/Build.ps1 -OutputDirectory artifacts/verification
#>
[CmdletBinding()]
param([switch]$Restore, [string]$OutputDirectory = 'artifacts/publish')
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'AiNexus') -Force

$root = Get-NexusRoot
$publish = Resolve-NexusArtifactPath $OutputDirectory
$env:NG_CLI_ANALYTICS = 'false'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
Push-Location -LiteralPath $root
try {
    if ($Restore) { & (Join-Path $PSScriptRoot 'Restore.ps1') }
    npm --prefix frontend run build
    if ($LASTEXITCODE -ne 0) { throw '前端 build 失敗。' }
    dotnet publish backend/src/AiNexus.Host/AiNexus.Host.csproj --no-restore -c Release -o $publish
    if ($LASTEXITCODE -ne 0) { throw '後端 publish 失敗。' }
    $webRoot = Join-Path $publish 'wwwroot'
    if (Test-Path -LiteralPath $webRoot) {
        $resolved = (Resolve-Path -LiteralPath $webRoot).ProviderPath
        $inside = $resolved -eq [IO.Path]::GetFullPath($webRoot) -and $resolved.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)
        if (!$inside -or ((Get-Item -LiteralPath $resolved).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw '拒絕刪除此 repo 以外或經由連結的 wwwroot。'
        }
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
    New-Item -ItemType Directory -Path $webRoot -Force | Out-Null
    Get-ChildItem -LiteralPath (Join-Path $root 'frontend/dist/ai-nexus/browser') | Copy-Item -Destination $webRoot -Recurse -Force
    Write-Output "發布完成：$publish"
} finally { Pop-Location }
