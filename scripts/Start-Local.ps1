#requires -Version 7.4
param([switch]$SkipBuild, [ValidateRange(1024,65535)][int]$Port = 5080)
$ErrorActionPreference = 'Stop'
$taskRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'Local-Settings.ps1')
Initialize-NexusLocalSettings
$taskLocal = Get-NexusLocalPaths
if (!$SkipBuild) { & (Join-Path $PSScriptRoot 'Build.ps1'); if ($LASTEXITCODE -ne 0) { throw 'Build failed.' } }
$taskPublish = Join-Path $taskRoot 'artifacts/publish'
$taskDll = Join-Path $taskPublish 'AiNexus.Api.dll'
if (!(Test-Path -LiteralPath $taskDll)) { throw 'Run scripts/Build.ps1 first.' }
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet $taskDll --contentRoot $taskPublish --urls "http://localhost:$Port" --LocalConfigPath $taskLocal.Settings --SecretsConfigPath $taskLocal.Secrets
