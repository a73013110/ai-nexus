#requires -Version 7.4

<#
.SYNOPSIS
建置 Angular 並發布 .NET，產出可直接執行的網站。

.DESCRIPTION
先 build 前端，再 dotnet publish 主機，最後把前端產物放進發布目錄的 wwwroot。
輸出目錄必須在 artifacts/ 內，publish 前會先清空（不是連結時才清）；舊的 wwwroot 只有在確認是此 repo 內的實體目錄時才會刪除。

.PARAMETER Restore
先執行 Restore.ps1。

.PARAMETER OutputDirectory
發布目錄，相對於 repo 根目錄，必須在 artifacts/ 內。

.PARAMETER Runtime
發布的平台，只帶該平台的原生檔；預設為執行這個腳本的平台。IIS 套件固定 win-x64（Publish-IIS.ps1）。

.COMPONENT
建置與檢查

.EXAMPLE
./scripts/Build.ps1 -Restore

.EXAMPLE
./scripts/Build.ps1 -OutputDirectory artifacts/verification
#>
[CmdletBinding()]
param([switch]$Restore, [string]$OutputDirectory = 'artifacts/publish', [ValidateSet('win-x64', 'linux-x64')][string]$Runtime = $(if ($IsWindows) { 'win-x64' } else { 'linux-x64' }))
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'AiNexus') -Force

$root = Get-RepoRoot
$publish = Resolve-RepoArtifactPath $OutputDirectory
Disable-RepoTelemetry
Push-Location -LiteralPath $root
$scripts = $PSScriptRoot
try {
    Invoke-RepoSteps '建置網站' @(
        @{ Name = '還原套件'; Skip = $(if (!$Restore) { '未加 -Restore' }); Run = { & (Join-Path $scripts 'Restore.ps1') } }
        @{ Name = '前端 build'; Run = {
                npm --prefix frontend run build
                if ($LASTEXITCODE -ne 0) { throw '前端 build 失敗。' }
            }
        }
        @{ Name = '後端 publish'; Run = {
                # publish -o 只覆寫不清除；先清空，否則已不再依賴的檔案（如舊的 .playwright）會留在發布包裡。
                if (Test-Path -LiteralPath $publish) {
                    if ((Get-Item -LiteralPath $publish).Attributes -band [IO.FileAttributes]::ReparsePoint) {
                        throw '拒絕清空經由連結的輸出目錄。'
                    }
                    Remove-Item -LiteralPath $publish -Recurse -Force
                }
                dotnet publish backend/src/AiNexus.Host/AiNexus.Host.csproj --no-restore -c Release -r $Runtime --self-contained false -o $publish
                if ($LASTEXITCODE -ne 0) { throw '後端 publish 失敗。' }
            }
        }
        @{ Name = '放入前端產物'; Run = {
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
            }
        }
    )
    Write-Output "發布完成：$publish"
} finally { Pop-Location }
