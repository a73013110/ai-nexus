#requires -Version 7.4
param([switch]$SkipBuild, [switch]$Http, [ValidateRange(1024,65535)][int]$Port = 5080, [string]$PublishDirectory = $(if ($env:NEXUS_E2E_PUBLISH_DIRECTORY) { $env:NEXUS_E2E_PUBLISH_DIRECTORY } else { 'artifacts/publish' }))
$ErrorActionPreference = 'Stop'
$taskRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'Local-Settings.ps1')
. (Join-Path $PSScriptRoot 'Local-Https.ps1')
if (!$Http) { Assert-NexusHttpsCertificate }
Initialize-NexusLocalSettings
$taskLocal = Get-NexusLocalPaths
if (!$SkipBuild) { & (Join-Path $PSScriptRoot 'Build.ps1') -OutputDirectory $PublishDirectory; if ($LASTEXITCODE -ne 0) { throw 'Build failed.' } }
$taskPublish = [System.IO.Path]::GetFullPath($PublishDirectory, $taskRoot)
$taskDll = Join-Path $taskPublish 'AiNexus.Host.dll'
if (!(Test-Path -LiteralPath $taskDll)) { throw 'Run scripts/Build.ps1 first.' }
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$taskScheme = if ($Http) { 'http' } else { 'https' }
dotnet $taskDll --contentRoot $taskPublish --urls "${taskScheme}://localhost:$Port" --Security:AllowInsecureLocalhost $Http.IsPresent --LocalConfigPath $taskLocal.Settings --SecretsConfigPath $taskLocal.Secrets
