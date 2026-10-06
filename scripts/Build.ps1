#requires -Version 7.4
param([switch]$Restore, [string]$OutputDirectory = 'artifacts/publish')
$ErrorActionPreference = 'Stop'
$taskRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$taskPublish = [System.IO.Path]::GetFullPath($OutputDirectory, $taskRoot)
$taskArtifactsPrefix = (Join-Path $taskRoot 'artifacts') + [System.IO.Path]::DirectorySeparatorChar
if (!$taskPublish.StartsWith($taskArtifactsPrefix, [System.StringComparison]::OrdinalIgnoreCase)) { throw 'Build output must be a directory inside workspace artifacts/.' }
$env:NG_CLI_ANALYTICS = 'false'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
Push-Location -LiteralPath $taskRoot
try {
    if ($Restore) {
        & (Join-Path $PSScriptRoot 'Restore.ps1')
    }
    npm --prefix frontend run build
    if ($LASTEXITCODE -ne 0) { throw 'Frontend build failed.' }
    dotnet publish backend/src/AiNexus.Api/AiNexus.Api.csproj --no-restore -c Release -o $taskPublish
    if ($LASTEXITCODE -ne 0) { throw 'Backend publish failed.' }
    $taskWebRoot = Join-Path $taskPublish 'wwwroot'
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
    Write-Output "Local publish ready: $taskPublish"
} finally { Pop-Location }
