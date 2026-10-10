#requires -Version 7.4

<#
.SYNOPSIS
以 .local/ 的設定建立資料庫（不存在時）並套用 migration。

.DESCRIPTION
執行主機的 db init 指令。只在資料庫不存在時建庫，不會刪除或清空既有資料；需要有建庫／DDL 權限的 SQL 登入。
正式環境改用主機指令，見 docs/operations/IIS_DEPLOYMENT.md。

.EXAMPLE
./scripts/Initialize-Database.ps1
#>
[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'AiNexus') -Force

if ((Invoke-NexusHostCommand 'db', 'init') -ne 0) { throw '資料庫初始化未完成。請確認 SQL 連線、憑證與建庫／DDL 權限。' }
