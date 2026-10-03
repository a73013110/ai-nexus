#requires -Version 7.4
$ErrorActionPreference = 'Stop'
$taskRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$taskProject = Join-Path $taskRoot 'backend/src/AiNexus.Api/AiNexus.Api.csproj'
. (Join-Path $PSScriptRoot 'Local-Settings.ps1')
Initialize-NexusLocalSettings
$taskLocal = Get-NexusLocalPaths
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project $taskProject --no-launch-profile -- --InitializeDatabase true --LocalConfigPath $taskLocal.Settings --SecretsConfigPath $taskLocal.Secrets
if ($LASTEXITCODE -ne 0) { throw '資料庫初始化未完成。請確認 SQL 連線、憑證與建庫／DDL 權限。' }
