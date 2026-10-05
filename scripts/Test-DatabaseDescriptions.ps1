#requires -Version 7.4
$ErrorActionPreference = 'Stop'
$taskRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'Local-Settings.ps1')
Initialize-NexusLocalSettings
$taskLocal = Get-NexusLocalPaths
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project (Join-Path $taskRoot 'backend/src/AiNexus.Api') --no-launch-profile -- --VerifyDatabaseDescriptions true --LocalConfigPath $taskLocal.Settings --SecretsConfigPath $taskLocal.Secrets
if ($LASTEXITCODE -ne 0) { throw '資料庫描述檢查失敗。請套用最新 migration，並檢查 MS_Description。' }
