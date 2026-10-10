#requires -Version 7.4

<#
.SYNOPSIS
從執行中的 Development API 重產 contracts/openapi.json 與前端 schema.ts。

.DESCRIPTION
需要先以 Start-Local.ps1 或 Start-Dev.ps1 啟動網站；/openapi/v1.json 只在 Development 提供，不需要登入。
兩個產出都是產生物，不要手改；API 有變動時一起提交。

.PARAMETER BaseUrl
執行中網站的網址。

.EXAMPLE
./scripts/Export-Contracts.ps1 -BaseUrl https://localhost:5080
#>
[CmdletBinding()]
param([string]$BaseUrl = 'https://localhost:5080')
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'AiNexus') -Force

$root = Get-NexusRoot
Invoke-WebRequest -Uri "$($BaseUrl.TrimEnd('/'))/openapi/v1.json" -OutFile (Join-Path $root 'contracts/openapi.json') -TimeoutSec 10
npm --prefix (Join-Path $root 'frontend') run contracts
if ($LASTEXITCODE -ne 0) { throw '前端型別產生失敗。' }
