#requires -Version 7.4
$ErrorActionPreference = 'Stop'
$taskRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'Local-Settings.ps1')
Initialize-NexusLocalSettings
$taskLocal = Get-NexusLocalPaths
$taskReport = Join-Path $taskRoot 'artifacts/connection-checks.json'
New-Item -ItemType Directory -Path (Split-Path $taskReport) -Force | Out-Null
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project (Join-Path $taskRoot 'backend/src/AiNexus.Api/AiNexus.Api.csproj') --no-launch-profile -- --VerifyConnections true --LocalConfigPath $taskLocal.Settings --SecretsConfigPath $taskLocal.Secrets --VerificationOutput $taskReport
if ($LASTEXITCODE -ne 0) { throw '連線檢查未全部通過。請查看 artifacts/connection-checks.json 的安全診斷。' }
