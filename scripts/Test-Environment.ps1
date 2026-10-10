#requires -Version 7.4

<#
.SYNOPSIS
以 .local/ 的設定實測這台機器的 SQL、AD 與模型服務。

.DESCRIPTION
先執行主機的 verify sql（SQL Server 版本、原生向量、全文檢索與繁中斷詞），再執行 verify connections
（以合成資料實測 SQL 讀寫、AD 服務帳號、模型串流、附件辨識與檢索模型，會使用模型配額）。
報告寫在 artifacts/sql-capabilities.json 與 artifacts/connection-checks.json，不含秘密。與自動化測試分開執行。

.PARAMETER SqlOnly
只檢查 SQL 能力，不呼叫 AD 與模型。

.EXAMPLE
./scripts/Test-Environment.ps1

.EXAMPLE
./scripts/Test-Environment.ps1 -SqlOnly
#>
[CmdletBinding()]
param([switch]$SqlOnly)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'AiNexus') -Force

$artifacts = Join-Path (Get-NexusRoot) 'artifacts'
New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
$sqlReport = Join-Path $artifacts 'sql-capabilities.json'
if ((Invoke-NexusHostCommand 'verify', 'sql', '--output', $sqlReport) -ne 0) { throw 'SQL 能力檢查失敗；未輸出任何帳密。' }
$capabilities = Read-NexusJson $sqlReport
if (!$capabilities['FullTextInstalled'] -or !$capabilities['TraditionalChineseWordBreaker']) {
    Write-Warning 'SQL 全文元件或繁中 1028 斷詞器不可用；混合檢索會改用 vector 模式。'
}
if ($SqlOnly) { return }
if ((Invoke-NexusHostCommand 'verify', 'connections', '--output', (Join-Path $artifacts 'connection-checks.json')) -ne 0) {
    throw '連線檢查未全部通過；請查看 artifacts/connection-checks.json 的安全診斷。'
}
