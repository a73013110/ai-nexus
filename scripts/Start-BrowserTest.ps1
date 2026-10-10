#requires -Version 7.4
param(
    [ValidateRange(1024,65535)][int]$Port = 5180,
    [string]$PublishDirectory = $(if ($env:NEXUS_E2E_PUBLISH_DIRECTORY) { $env:NEXUS_E2E_PUBLISH_DIRECTORY } else { 'artifacts/publish' })
)
$ErrorActionPreference = 'Stop'
$taskRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$taskPublish = [IO.Path]::GetFullPath($PublishDirectory, $taskRoot)
$taskArtifacts = Join-Path $taskRoot 'artifacts'
if (!$taskPublish.StartsWith($taskArtifacts + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Browser-test publish directory must be inside workspace artifacts/.' }
$taskDll = Join-Path $taskPublish 'AiNexus.Host.dll'
if (!(Test-Path -LiteralPath $taskDll)) { throw 'Run scripts/Build.ps1 before browser tests.' }
# Explicit isolated paths prevent discovery of machine .local or external Production credentials.
# Development retains the existing loopback-only HTTP policy; API data is provided by test fixtures.
$taskConfig = Join-Path $taskArtifacts 'browser-server-config'
New-Item -ItemType Directory -Path $taskConfig -Force | Out-Null
$taskSettings = Join-Path $taskConfig 'appsettings.Local.json'
$taskSecrets = Join-Path $taskConfig 'appsettings.Secrets.json'
[IO.File]::WriteAllText($taskSettings, '{}')
[IO.File]::WriteAllText($taskSecrets, '{}')
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ConnectionStrings__Nexus = ''
$env:Database__User = ''
$env:Database__Password = ''
$env:Database__Server = '127.0.0.1,1'
$env:Database__ApplyMigrationsOnStartup = 'false'
$env:Identity__ActiveDirectory__Mode = 'Windows'
$env:DataProtection__KeyRingPath = Join-Path $taskArtifacts 'browser-server-keys'
$env:Attachments__StoragePath = Join-Path $taskArtifacts 'browser-server-attachments'
$env:Diagnostics__Directory = Join-Path $taskArtifacts 'browser-server-diagnostics'
$env:Diagnostics__OtlpEnabled = 'false'
$env:Diagnostics__SqlTimeoutSeconds = '1'
dotnet $taskDll --contentRoot $taskPublish --urls "http://localhost:$Port" --Security:AllowInsecureLocalhost true --LocalConfigPath $taskSettings --SecretsConfigPath $taskSecrets
exit $LASTEXITCODE
