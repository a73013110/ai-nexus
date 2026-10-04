#requires -Version 7.4
$ErrorActionPreference = 'Stop'
$taskRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'Local-Settings.ps1')
Initialize-NexusLocalSettings
$taskLocal = Get-NexusLocalPaths
$taskOutput = Join-Path $taskRoot 'artifacts/sql-capabilities.json'
New-Item -ItemType Directory -Path (Split-Path $taskOutput) -Force | Out-Null
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project (Join-Path $taskRoot 'backend/src/AiNexus.Api') --no-launch-profile -- --VerifySqlCapabilities true --VerificationOutput $taskOutput --LocalConfigPath $taskLocal.Settings --SecretsConfigPath $taskLocal.Secrets
if ($LASTEXITCODE -ne 0) { throw 'SQL capability verification failed; credentials were not printed.' }
