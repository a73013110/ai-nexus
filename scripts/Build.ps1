#requires -Version 7.4
param([switch]$Restore)
$ErrorActionPreference = 'Stop'
$taskRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$env:NG_CLI_ANALYTICS = 'false'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
Push-Location -LiteralPath $taskRoot
try {
    if ($Restore) {
        & (Join-Path $PSScriptRoot 'Restore.ps1')
    }
    npm --prefix frontend run build
    if ($LASTEXITCODE -ne 0) { throw 'Frontend build failed.' }
    dotnet publish backend/src/AiNexus.Api/AiNexus.Api.csproj --no-restore -c Release -o artifacts/publish
    if ($LASTEXITCODE -ne 0) { throw 'Backend publish failed.' }
    $taskWebRoot = Join-Path $taskRoot 'artifacts/publish/wwwroot'
    if (Test-Path -LiteralPath $taskWebRoot) {
        $taskResolvedWebRoot = (Resolve-Path -LiteralPath $taskWebRoot).ProviderPath
        $taskExpectedWebRoot = [System.IO.Path]::GetFullPath($taskWebRoot)
        $taskRootPrefix = $taskRoot + [System.IO.Path]::DirectorySeparatorChar
        if ($taskResolvedWebRoot -ne $taskExpectedWebRoot -or !$taskResolvedWebRoot.StartsWith($taskRootPrefix, [System.StringComparison]::OrdinalIgnoreCase) -or ((Get-Item -LiteralPath $taskResolvedWebRoot).Attributes -band [System.IO.FileAttributes]::ReparsePoint)) {
            throw 'Refusing to replace a web root outside this workspace or through a link.'
        }
        Remove-Item -LiteralPath $taskResolvedWebRoot -Recurse -Force
    }
    New-Item -ItemType Directory -Path $taskWebRoot -Force | Out-Null
    Get-ChildItem -LiteralPath (Join-Path $taskRoot 'frontend/dist/ai-nexus/browser') | Copy-Item -Destination $taskWebRoot -Recurse -Force
    Write-Output "Local publish ready: $(Join-Path $taskRoot 'artifacts/publish')"
} finally { Pop-Location }
